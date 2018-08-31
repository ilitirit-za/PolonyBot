using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using System.IO;

namespace PolonyBot.Modules.LFG
{
    // Create a module with no prefix
    public class LfgModule : ModuleBase
    {
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
        private static readonly List<string> fgUserGameList = new List<string> { };
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
                response = await ListPlayersLookingForGamesAsync();
                await Context.User.SendMessageAsync(response);
            }
            else if (game == "?")
            {
                response = ListSupportedGames();
                await Context.User.SendMessageAsync($"```{response}```");
            }
            else if (game == "help")
            {
                response = GetHelpMessage();
                await Context.User.SendMessageAsync(response);
            }
            else if (game == "-")
            {
                LfgList.RemoveAll(x => x.User.Id == Context.User.Id);
                await Context.User.SendMessageAsync($"You have been removed from all LFG queues");
            }
            else
            {
                response = await RegisterPlayerAsync(Context.User, game, (command ?? "").Trim());
                await ReplyAsync(response);
            }
        }

        private async Task<string> ListGuildUsersPlayingAsync(string game = null, bool excludeCurrentUser = true)
        {
            var guildUsers = await Context.Guild.GetUsersAsync();       //Retrieve all users (+ statuses) from server.
            var users = guildUsers
                .Where(user => !user.IsBot) // No bots
                .Where(user => user.Id != Context.User.Id); // Ignore current user

            var response = "";
            var gameLabel = ConvertGameNameToLabel(game);
            if (gameLabel != GameLabel.BlankLabel)
            {
                var filteredUsers = guildUsers.Where(u => u.Activity?.Name == gameLabel.UserStatusLabel);
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
                var filteredUsers = guildUsers.Where(u => fgUserGameList.Contains(u.Activity?.Name)).OrderBy(u => (u.Activity?.Name ?? ""));
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

        private void LoadGameList()
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
                    if (!String.IsNullOrEmpty(split[2]) || !fgUserGameList.Contains(split[2]))
                    {
                        fgUserGameList.Add(split[2]);
                    }
                }
            }
            catch (Exception e)
            {
                ReplyAsync($"Could not load game list.  Tell ilitirit about this! ({e.Message})");
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

        private async Task<string> RegisterPlayerAsync(IUser user, string game, string command)
        {
            if (!_games.TryGetValue(game, out GameLabel description))
            {
                return $"Game {game} is not supported. Use the \"lfg ?\" command to list supported games";
            }
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

            response += await ListPlayersLookingForGamesAsync(game, true, true);
            response += Environment.NewLine;

            return response;

        }

        private async Task<string> ListPlayersLookingForGamesAsync(string game = null, bool excludeCurrentUser = false, bool enableMentions = false)
        {
            var response = "";
            var gameFilter = (game == null) ? (Func<string, bool>)((x) => true) : ((x) => x == game);
            var userFilter = excludeCurrentUser ? (Func<LfgEntry, bool>)((x) => x.User.Id != Context.User.Id) : ((x) => true);

            foreach (var key in _games.Keys.Where(gameFilter))
            {
                var users = LfgList
                    .Where(x => x.Game.Equals(key))
                    .Where(userFilter)
                    .Select(lfg => (lfg.AutoMention && enableMentions && lfg.LastMentioned < DateTime.Now.AddMinutes(-10)
                        ? lfg.User.Mention
                        : lfg.User.Username) + $" [{Math.Ceiling((lfg.Expiry - DateTime.Now).TotalMinutes)} mins]")
                    .ToList();

                if (users != null && users.Count > 0)
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

            response += await ListGuildUsersPlayingAsync(game);

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
    }
}
