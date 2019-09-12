using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using System.IO;
using Discord.WebSocket;
using PolonyBot.Modules.LFG.DAL;
using PolonyBot.Modules.LFG.Utils;

namespace PolonyBot.Modules.LFG
{
    public class LfgModule : ModuleBase
    {
        private const int MessageLengthLimit = 1980;

        private class GameLabel
        {
            public static readonly GameLabel BlankLabel = new GameLabel { Label = "", UserStatusLabel = "" };
            public string Label { get; set; }
            public string UserStatusLabel { get; set; }
         
            public override string ToString()
            {
                return Label;
            }
        }

        private readonly Dictionary<string, GameLabel> _games = new Dictionary<string, GameLabel>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string> FgUserGameList = new List<string>();
        private static LfgDao _dao = new LfgDao();

        public LfgModule()
        {
            LoadGameList();
            
            _dao.Init();
        }

        private static readonly List<LfgEntry> LfgList = new List<LfgEntry>();

        [Command("lfg"), Summary("Looking for games")]
        public async Task Lfg(string game = null, string command = null)
        {
            var response = "";

            LfgList.RemoveAll(x => x.Expiry < DateTime.Now);

            if (String.IsNullOrWhiteSpace(game))
            {
                response = await ListPlayersLookingForGamesAsync().ConfigureAwait(false);
                await _dao.InsertCommand(Context.User.Id, Context.User.Username, "LIST-QUEUES", "").ConfigureAwait(false);
                await CustomSendMessageAsync(response).ConfigureAwait(false);
            }
            else switch (game)
            {
                case "stats":
                    var guildUser = Context.User as SocketGuildUser;
                    if (guildUser?.Roles.Any(r => r.Name == "PolonyBot-Dev" || r.Name == "PolonyBot-Tester" || r.Name == "Moderator") == true)
                    {
                        var statsTableResponse = await GetStats(command).ConfigureAwait(false);
                        foreach (var table in statsTableResponse)
                        {
                            response = table;
                            await _dao.InsertCommand(Context.User.Id, Context.User.Username, "STATS", "").ConfigureAwait(false);
                            await CustomSendMessageAsync($"```{response}```").ConfigureAwait(false);
                        }
                    }
                    break;

                case "?":
                    response = ListSupportedGames();
                    await _dao.InsertCommand(Context.User.Id, Context.User.Username, "LIST-SUPPORTED-GAMES", "").ConfigureAwait(false);
                    await CustomSendMessageAsync($"```{response}```").ConfigureAwait(false);
                    break;
                case "help":
                    response = GetHelpMessage();
                    await _dao.InsertCommand(Context.User.Id, Context.User.Username, "HELP", "").ConfigureAwait(false);
                    await CustomSendMessageAsync(response).ConfigureAwait(false);
                    break;
                case "-":
                    LfgList.RemoveAll(x => x.User.Id == Context.User.Id);
                    await _dao.InsertCommand(Context.User.Id, Context.User.Username, "REMOVE", "").ConfigureAwait(false);
                    await CustomSendMessageAsync($"You have been removed from all LFG queues").ConfigureAwait(false);
                    break;
                default:
                    if (!_games.TryGetValue(game, out GameLabel description))
                    {
                        response = $"Game {game} is not supported. Use the \"lfg ?\" command to list supported games";
                    }
                    else
                    {
                        response = await RegisterPlayerAsync(Context.User, game, description, (command ?? "").Trim()).ConfigureAwait(false);
                        if (command == "-")
                            await _dao.InsertCommand(Context.User.Id, Context.User.Username, "REMOVE", game).ConfigureAwait(false);
                        else
                            await _dao.InsertCommand(Context.User.Id, Context.User.Username, "ADD", game).ConfigureAwait(false);
                    }

                    await CustomReplyAsync(response).ConfigureAwait(false);
                    break;
                }
        }

        private async Task<List<string>> GetStats(string statsCommand)
        {
            var tablesToRender = new List<DataTable>();
            var statsTable = await _dao.GetGeneralStats();

            // Very naive implementation but IDC

            // If we reach this number of iterations in the
            // loop then something is probably wrong
            var hardLimit = 100;
            var iteration = 0;
            
            var tableDataIsBeingMovedTo = default(DataTable);
            tablesToRender.Add(statsTable);

            while (iteration++ < hardLimit &&
                   AsciiTableGenerator.GetEstimatedTableSizeInCharacters(statsTable) > MessageLengthLimit)
            {
                if (tableDataIsBeingMovedTo == null)
                {
                    tableDataIsBeingMovedTo = statsTable.Clone();
                }

                var lastRowIndex = statsTable.Rows.Count - 1;
                var destinationRow = tableDataIsBeingMovedTo.NewRow();
                var sourceRow = statsTable.Rows[lastRowIndex];
                destinationRow.ItemArray = (object[])sourceRow.ItemArray.Clone();
                tableDataIsBeingMovedTo.Rows.InsertAt(destinationRow, 0);
                statsTable.Rows.RemoveAt(lastRowIndex);

                if (AsciiTableGenerator.GetEstimatedTableSizeInCharacters(tableDataIsBeingMovedTo) > MessageLengthLimit)
                {
                    tablesToRender.Add(tableDataIsBeingMovedTo);
                    tableDataIsBeingMovedTo = null;
                }
            }

            if (tableDataIsBeingMovedTo != null)
            {
                tablesToRender.Add(tableDataIsBeingMovedTo);
            }

            var tables = new List<string>();
            foreach (var dataTable in tablesToRender)
            {
                if (dataTable.Rows.Count > 0)
                    tables.Add(AsciiTableGenerator.CreateAsciiTableFromDataTable(dataTable).ToString());
            }

            return tables;
        }

        private async Task<string> ListGuildUsersPlayingAsync(string game = null)
        {
            // Retrieve all users (+ statuses) from server.
            var guildUsers = await Context.Guild.GetUsersAsync().ConfigureAwait(false);

            var response = "";
            var gameLabel = ConvertGameNameToLabel(game);
            if (gameLabel != GameLabel.BlankLabel)
            {
                var filteredUsers = guildUsers
                    .Where(u => u.Activity?.Name == gameLabel.UserStatusLabel)
                    .Where(user => user.Id != Context.User.Id)
                    .ToList();

                if (filteredUsers.Any())
                {
                    response += $"The following players are playing {gameLabel}: " + Environment.NewLine;
                    foreach (var user in filteredUsers)
                    {
                        response += user.Username + Environment.NewLine;
                    }
                }
            }
            else
            {
                var filteredUsers = guildUsers.Where(u => FgUserGameList.Contains(u.Activity?.Name)).OrderBy(u => (u.Activity?.Name ?? ""));
                response += $"The following players are playing: " + Environment.NewLine;
                foreach (var user in filteredUsers)
                {
                    response += $"{user.Username} ({user.Activity})" + Environment.NewLine;
                }
            }

            return response;
        }

        private GameLabel ConvertGameNameToLabel(string game)
        {
            if (game == null || !_games.TryGetValue(game, out var gameLabel))
                return GameLabel.BlankLabel;

            return gameLabel;
        }

        private async void LoadGameList()
        {
            _games.Clear();
            try
            {
                var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "games.txt"));
                foreach (var line in lines)
                {
                    var split = line.Split('|');
                    split[0] = split[0].Trim();
                    split[1] = split[1].Trim();
                    split[2] = split[2].Trim();

                    _games.Add(split[0], new GameLabel { Label = split[1], UserStatusLabel = split[2] });
                    if (!String.IsNullOrEmpty(split[2]) || !FgUserGameList.Contains(split[2]))
                    {
                        FgUserGameList.Add(split[2]);
                    }
                }
            }
            catch (Exception e)
            {
                await CustomReplyAsync($"Could not load game list.  Tell ilitirit or pwNBait about this! ({e.Message})")
                    .ConfigureAwait(false);
            }

        }

        private string GetHelpMessage()
        {
            var response =
                ".lfg           Display all players looking for games" + Environment.NewLine +
                ".lfg ?         Display supported games" + Environment.NewLine +
                ".lfg help      Display this help message" + Environment.NewLine +
                ".lfg [game]    Add yourself as a player looking for [game] games" + Environment.NewLine +
                ".lfg [game] +  Add yourself as player looking for [game] games and get mentioned automatically" + Environment.NewLine +
                ".lfg [game] -  Remove yourself as player looking for [game] games" + Environment.NewLine +
                ".lfg -         Remove yourself as player looking for any games" + Environment.NewLine +
                "" + Environment.NewLine +
                "Notes:" + Environment.NewLine +
                "- Do not include the square brackets ([]) when specifying the game" + Environment.NewLine +
                "- Player registration for a game expires after 2 hours by default" + Environment.NewLine +
                "- When auto-mention is enabled, you will only get mentioned once every 10 minutes for all games" + Environment.NewLine +
                "- The value in square brackets next to the users name indicates when their request for games expires" + Environment.NewLine;

            return $"```{response}```";
        }

        private async Task<string> RegisterPlayerAsync(IUser user, string game, GameLabel description, string command)
        {
            game = game.ToUpper();

            LfgList.RemoveAll(x => x.User.Id == Context.User.Id && x.Game == game);

            if (command == "-")
            {
                return $"{Context.User.Username} is no longer looking for {description} games";
            }

            LfgList.Add(new LfgEntry
            {
                Game = game,
                User = user,
                Expiry = DateTime.Now.AddHours(2),
                AutoMention = (command ?? "").StartsWith("+"),
                LastMentioned = new DateTime(),
            });

            var response = $"{user.Username} is now looking for {description} games";
            response += Environment.NewLine;
            response += Environment.NewLine;

            response += await ListPlayersLookingForGamesAsync(game, true, true).ConfigureAwait(false);
            response += Environment.NewLine;

            return response;

        }

        private async Task<string> ListPlayersLookingForGamesAsync(string game = null, bool excludeCurrentUser = false, bool enableMentions = false)
        {
            var response = "";
            var gameFilter = (game == null) ? (Func<string, bool>)((x) => true) : ((x) => x == game);
            var userFilter = excludeCurrentUser ? (Func<LfgEntry, bool>)((x) => x.User.Id != Context.User.Id) : x => true;

            foreach (var key in _games.Keys.Where(gameFilter))
            {
                var users = LfgList
                    .Where(x => x.Game.Equals(key))
                    .Where(userFilter)
                    .Select(lfg => (lfg.AutoMention && enableMentions && lfg.LastMentioned < DateTime.Now.AddMinutes(-10)
                        ? lfg.User.Mention
                        : lfg.User.Username) + $" [{Math.Ceiling((lfg.Expiry - DateTime.Now).TotalMinutes)} mins]")
                    .ToList();

                if (users.Count > 0)
                {
                    response += $"{_games[key]}: " + users.Aggregate((current, next) => current + " " + next);
                    response += Environment.NewLine;
                }

                if (enableMentions)
                {
                    foreach (var user in users)
                    {
                        foreach (var lfgEntry in LfgList)
                        {
                            if (lfgEntry.User.Username == user || lfgEntry.User.Mention == user)
                            {
                                lfgEntry.LastMentioned = DateTime.Now;
                            }
                        }
                    }
                }
            }

            var extra = excludeCurrentUser ? " else " : " ";
            if (String.IsNullOrWhiteSpace(response))
            {
                response = $"Noone{extra}is looking for games right now.";
                response += Environment.NewLine;
            }
            response += Environment.NewLine;

            response += await ListGuildUsersPlayingAsync(game).ConfigureAwait(false);

            return response;
        }

        private string ListSupportedGames()
        {
            var response = "The following games are supported:";
            response += Environment.NewLine;

            foreach (var key in _games.Keys)
            {
                response += $"{key} : {_games[key]}";
                response += Environment.NewLine;
            }

            return response;
        }

        private Task<IUserMessage> CustomSendMessageAsync(string message)
        {
            return Context.User.SendMessageAsync(message.AsDiscordResponse());
        }

        private Task<IUserMessage> CustomReplyAsync(string message)
        {
            return ReplyAsync(message.AsDiscordResponse());
        }
    }
}
