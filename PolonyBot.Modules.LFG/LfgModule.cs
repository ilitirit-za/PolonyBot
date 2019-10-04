using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using System.IO;
using System.Runtime.CompilerServices;
using PolonyBot.Modules.LFG.DAL;
using PolonyBot.Modules.LFG.Utils;
using Discord.Net;

[assembly:InternalsVisibleTo("PolonyBot.UnitTests")]
namespace PolonyBot.Modules.LFG
{
    public class LfgModule : ModuleBase
    {
        public const int MaxMessageLength = 1980;
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

        private ILfgDao _dao;
        internal ILfgDao Dao
        {
            get
            {
                if (_dao == null)
                {
                    _dao = new LfgDao();
                    _dao.Init();
                }

                return _dao;
            }

            set => _dao = value;
        }

        private ICommandContext _commandContext;
        internal ICommandContext CommandContext
        {
            get
            {
                if (_commandContext == null)
                {
                    CommandContext = Context;
                }

                return _commandContext;
            }

            set => _commandContext = value;
        }

        public LfgModule()
        {
            LoadGameList();
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
                await Dao.InsertCommand(CommandContext.User.Id, CommandContext.User.Username, "LIST-QUEUES", "").ConfigureAwait(false);
                await CustomSendMessageAsync(response).ConfigureAwait(false);
            }
            else switch (game)
            {
                case "stats":
                    var guildUser = CommandContext.User as IGuildUser;
                    var validRoles = CommandContext.Guild.Roles
                        .Where(r => r.Name == "PolonyBot-Dev" || r.Name == "PolonyBot-Tester" || r.Name == "Moderator");

                    if (guildUser?.RoleIds.Any(r => validRoles.Any(v => v.Id == r)) == true)
                    {
                        var statsTableResponse = await GetStats().ConfigureAwait(false);
                        foreach (var table in statsTableResponse)
                        {
                            response = table;
                            await CustomSendMessageAsync($"```{response}```").ConfigureAwait(false);
                        }
                        await Dao.InsertCommand(CommandContext.User.Id, CommandContext.User.Username, "STATS", "").ConfigureAwait(false);
                    }
                    break;

                case "?":
                    response = ListSupportedGames();
                    await Dao.InsertCommand(CommandContext.User.Id, CommandContext.User.Username, "LIST-SUPPORTED-GAMES", "").ConfigureAwait(false);
                    await CustomSendMessageAsync($"```{response}```", fallbackToChannelOnFail: true).ConfigureAwait(false);
                    break;
                case "help":
                    response = GetHelpMessage();
                    await Dao.InsertCommand(CommandContext.User.Id, CommandContext.User.Username, "HELP", "").ConfigureAwait(false);
                    await CustomSendMessageAsync(response, fallbackToChannelOnFail: true).ConfigureAwait(false);
                    break;
                case "-":
                    LfgList.RemoveAll(x => x.User.Id == CommandContext.User.Id);
                    await Dao.InsertCommand(CommandContext.User.Id, CommandContext.User.Username, "REMOVE", "").ConfigureAwait(false);
                    await CustomSendMessageAsync($"You have been removed from all LFG queues", fallbackToChannelOnFail: true).ConfigureAwait(false);
                    break;
                default:
                    if (!_games.TryGetValue(game, out GameLabel description))
                    {
                        response = $"Game {game} is not supported. Use the \"lfg ?\" command to list supported games";
                    }
                    else
                    {
                        response = await RegisterPlayerAsync(CommandContext.User, game, description, (command ?? "").Trim()).ConfigureAwait(false);
                        if (command == "-")
                            await Dao.InsertCommand(CommandContext.User.Id, CommandContext.User.Username, "REMOVE", game).ConfigureAwait(false);
                        else
                            await Dao.InsertCommand(CommandContext.User.Id, CommandContext.User.Username, "ADD", game).ConfigureAwait(false);
                    }

                    await CustomReplyAsync(response).ConfigureAwait(false);
                    break;
                }
        }

        public async Task<List<string>> GetStats()
        {
            var tablesToRender = new List<DataTable>();
            var statsTable = await Dao.GetGeneralStats();

            if (statsTable.Rows.Count == 0)
                return new List<string> { "No stats available" };

            var statsTableSize = AsciiTableGenerator.GetEstimatedTableSizeInCharacters(statsTable);

            var byteLimitSize = 20000;
            if (statsTableSize > byteLimitSize)
                throw new Exception($"Stats data estimation too big: {statsTableSize}");

            // The table will fit into one post so just return it
            if (statsTableSize < MaxMessageLength)
                return new List<string>
                {
                    AsciiTableGenerator.CreateAsciiTableFromDataTable(statsTable).ToString()
                };
            
            // If we reach this number of iterations in the
            // loop then something is probably wrong with the code
            // and/or data - we don't have 100's of games
            var hardLimit = 200;
            var iteration = 0;
            
            var bufferTable = statsTable.Clone();
            while (iteration++ < hardLimit)
            {
                // Nothing left to move
                if (statsTable.Rows.Count == 0)
                {
                    if (bufferTable.Rows.Count > 0)
                        tablesToRender.Add(bufferTable);

                    break;
                }

                bufferTable.Rows.Add(statsTable.Rows[0].ItemArray);
                statsTable.Rows.RemoveAt(0);

                var bufferTableSize = AsciiTableGenerator.GetEstimatedTableSizeInCharacters(bufferTable);
                if (bufferTableSize > MaxMessageLength)
                {
                    // Reverse the last addition
                    var lastRowIndex = bufferTable.Rows.Count - 1;
                    var reversedRow = statsTable.NewRow();
                    reversedRow.ItemArray = bufferTable.Rows[lastRowIndex].ItemArray;
                    statsTable.Rows.InsertAt(reversedRow, 0);
                    bufferTable.Rows.RemoveAt(lastRowIndex);

                    // The buffer table is full so add it to the list
                    // and create a new one
                    tablesToRender.Add(bufferTable);
                    bufferTable = statsTable.Clone();
                }
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
            var guildUsers = await CommandContext.Guild.GetUsersAsync().ConfigureAwait(false);

            var response = "";
            var gameLabel = ConvertGameNameToLabel(game);
            if (gameLabel != GameLabel.BlankLabel)
            {
                var filteredUsers = guildUsers
                    .Where(u => u.Activity?.Name == gameLabel.UserStatusLabel)
                    .Where(user => user.Id != CommandContext.User.Id)
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

            LfgList.RemoveAll(x => x.User.Id == CommandContext.User.Id && x.Game == game);

            if (command == "-")
            {
                return $"{CommandContext.User.Username} is no longer looking for {description} games";
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
            var userFilter = excludeCurrentUser ? (Func<LfgEntry, bool>)((x) => x.User.Id != CommandContext.User.Id) : x => true;

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

        private async Task CustomSendMessageAsync(string message, bool fallbackToChannelOnFail = false)
        {
            try
            {
                await CommandContext.User.SendMessageAsync(message.AsDiscordResponse());
            }
            catch (HttpException e)
            {
                if (e.DiscordCode == 50007)
                {
                    if (fallbackToChannelOnFail)
                    {
                        await CustomReplyAsync(message.AsDiscordResponse());
                    }
                    else
                    {
                        await CustomReplyAsync("Unable to respond to your command via DM. Please check your privacy settings.");
                    }
                }
                else
                {
                    throw e;
                }
            }
        }

        private Task<IUserMessage> CustomReplyAsync(string message)
        {
            return ReplyAsync(message.AsDiscordResponse());
        }
    }
}
