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

        //{
        //    { "2K2", "King Of Fighters 2002 on Fightcade" },
        //    { "3S", "Street Fighter 3: 3rd Strike on Fightcade" },
        //    { "98", "King Of Fighters 98 on Fightcade" },
        //    { "A2", "Street Fighter Alpha 2 on Fightcade" },
        //    { "A3", "Street Fighter Alpha 3 on Fightcade" },
        //    { "BB-PC", "Blazblue on PC" },
        //    { "BB-PS4", "Blazblue on PS4" },
        //    { "BB-X", "Blazblue on Xbox One" },
        //    { "FC", "Any game on Fightcade" },
        //    { "I2-PS4", "Injustice 2 on PS4" },
        //    { "I2-X", "Injustice 2 on Xbox One" },
        //    { "KOF13", "King of Fighters 13 on PC" },
        //    { "KOF14-PC", "King Of Fighters 14 on PC" },
        //    { "KOF14-PS4", "King Of Fighters 14 on PS4" },
        //    { "LB2", "Last Blade 2 on Fightcade" },
        //    { "MKX-PC", "Mortal Kombat X on PC" },
        //    { "MKX-PS4", "Mortal Kombat X on PS4" },
        //    { "MKX-X", "Mortal Kombat X on Xbox One" },
        //    { "SFV", "Street Fighter V" },
        //    { "SG", "Skullgirls on PC" },
        //    { "ST", "Super Street Fighter 2 Turbo on Fightcade" },
        //    { "SVC", "SNK vs Capcom Chaos on Fightcade" },
        //    { "T7-PC", "Tekken 7 on PC" },
        //    { "T7-PS4", "Tekken 7 on PS4" },
        //    { "T7-X", "Tekken 7 on Xbox One" },
        //    { "SF4-PC", "Ultra Street Fighter IV on PC" },
        //    { "SF4-PS4", "Ultra Street Fighter IV on PS4" },
        //    { "XRD-PC", "Guilty Gear Xrd on PC" },
        //    { "XRD-PS4", "Guilty Gear Xrd on PS4" },
        //};

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

        private async Task<string> ListGuildUsersPlayingAsync(string game = null, bool excludeCurrentUser = false)
        {
            
            var response = "";
            IReadOnlyCollection<IGuildUser> guildUsers = await Context.Guild.GetUsersAsync();       //Retrieve all users (+ statuses) from server.

            GameLabel gameLabel = GameLabel.BlankLabel;
            if (game != null)
            {
                //Convert game key to discord playing label
                _games.TryGetValue(game, out gameLabel);
            }

            //Filter out any bots.
            Func<IGuildUser, bool> botFilter = (x) => !(x.IsBot);

            //Filter out users not playing anything, not playing a fighting game or not playing requested game.
            Func<IGuildUser, bool> gameFilter = (game == null) ? ((x) => (!String.IsNullOrWhiteSpace(x.Game.ToString()) && fgUserGameList.Contains(x.Game.ToString()))) : gameFilter = (x) => x.Game.ToString() == gameLabel.UserStatusLabel;

            //Remove current user from list.
            Func<IGuildUser, bool> userFilter = (excludeCurrentUser) ? (Func<IGuildUser, bool>)((x) => x.DiscriminatorValue != Context.User.DiscriminatorValue) : ((x) => true);

            
            List<IGuildUser> users = guildUsers
                .Where(gameFilter)
                .Where(botFilter)
                .Where(userFilter)
                .ToList();


            IGuildUser u;
            switch (users.Count)
            {
                case 0:
                    break;
                case 1:
                    u = users.First();
                    response += $"{u.Username} is currently playing {u.Game}." + Environment.NewLine;
                    break;
                default:
                    u = users.First();
                    response += $"The following players are playing {gameLabel}: {u.Username}" + Environment.NewLine;
                    users.RemoveAt(0);
                    foreach (var user in users)
                    {
                        response += new String(' ',  gameLabel.UserStatusLabel.Length+63) + $"{user.Username}" + Environment.NewLine;
                    }
                    break;
            }
            
            return response;
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
                    if (!String.IsNullOrEmpty(split[2]) || !fgUserGameList.Contains(split[2])) {
                        fgUserGameList.Add(split[2]);
                    }
                }
            }
            catch (Exception e)
            {
                ReplyAsync("Could not load game list.  Tell ilitirit about this!");
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
            GameLabel description;
            if (!_games.TryGetValue(game, out description))
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
