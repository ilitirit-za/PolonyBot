using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Polony.Console
{
    class Program
    {
        
        private CommandService commands;
        private DiscordSocketClient client;
        private IServiceProvider services;

        public static void Main(string[] args) => new Program().Start().GetAwaiter().GetResult();

        public async Task Start()
        {
            await LoadGlossary();

            client = new DiscordSocketClient();
            commands = new CommandService();

            string token = "MjI5OTMzNTczMjk2MDk1MjMy.Ct1XmA.0X1Y8PnTrtEDF6nlHRXnqLVNmkI";

            services = new ServiceCollection()
                    .BuildServiceProvider();

            await InstallCommands();

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            await Task.Delay(-1);
        }

        public static Dictionary<string, string> Glossary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private async Task LoadGlossary()
        {
            var text = System.IO.File.ReadAllText("glossary.txt");

            var splitText = text.Split(new [] { $"{Environment.NewLine}==" }, StringSplitOptions.None);

            foreach (var entry in splitText)
            {
                var entryDef = entry.Split(new[] { $"==" }, StringSplitOptions.RemoveEmptyEntries);
                var term = entryDef[0].Trim();
                var definition = entryDef[1].Trim();

                Glossary.Add(term, definition);
            }
        }

        public async Task InstallCommands()
        {
            // Hook the MessageReceived Event into our Command Handler
            client.MessageReceived += HandleCommand;
            // Discover all of the commands in this assembly and load them.
            await commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        public async Task HandleCommand(SocketMessage messageParam)
        {
            // Don't process the command if it was a System Message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;
            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;
            // Determine if the message is a command, based on if it starts with '!' or a mention prefix
            if (!(message.HasCharPrefix('.', ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos))) return;
            // Create a Command Context
            var context = new CommandContext(client, message);
            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed successfully)
            var result = await commands.ExecuteAsync(context, argPos, services);
            if (!result.IsSuccess)
                await context.Channel.SendMessageAsync(result.ErrorReason);
        }
    }

    // Create a module with no prefix
    public class GlossaryModule : ModuleBase
    {
        // ~say hello -> hello
        [Command("define"), Summary("Returns the definition of an FG term.")]
        public async Task Define([Remainder, Summary("The term")] string term)
        {
            var response = String.Empty;
            if (!Program.Glossary.TryGetValue(term, out response))
            {
                var possible = Program.Glossary.Keys.ToAsyncEnumerable()
                    .Select(key => new Tuple<string, int>(key, LevenshteinDistance.Compute(term, key)))
                    .OrderBy(tuple => tuple.Item2)
                    .Take(3)
                    .Select(tuple => tuple.Item1)
                    .Aggregate((current, next) => $"{current}, {next}")
                    .Result;

                response = $"No definition found for {term}.  Suggestions: {possible}";
            }

            await ReplyAsync($"```{response}```");
        }
    }

    public class LfgEntry
    {
        public string Game { get; set; }
        public IUser User { get; set; }
        public bool AutoMention { get; set; }
        public DateTime Expiry { get; set; }
        public DateTime LastMentioned { get; set; }
    }

    // Create a module with no prefix
    public class LfgModule : ModuleBase
    {
        private readonly Dictionary<string, string> _games = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "2K2", "King Of Fighters 2002 on Fightcade" },
            { "3S", "Street Fighter 3: 3rd Strike on Fightcade" },
            { "98", "King Of Fighters 98 on Fightcade" },
            { "A2", "Street Fighter Alpha 2 on Fightcade" },
            { "A3", "Street Fighter Alpha 3 on Fightcade" },
            { "BB-PC", "Blazblue on PC" },
            { "BB-PS4", "Blazblue on PS4" },
            { "BB-X", "Blazblue on Xbox One" },
            { "FC", "Any game on Fightcade" },
            { "I2-PS4", "Injustice 2 on PS4" },
            { "I2-X", "Injustice 2 on Xbox One" },
            { "KOF13", "King of Fighters 13 on PC" },
            { "KOF14-PC", "King Of Fighters 14 on PC" },
            { "KOF14-PS4", "King Of Fighters 14 on PS4" },
            { "LB2", "Last Blade 2 on Fightcade" },
            { "MKX-PC", "Mortal Kombat X on PC" },
            { "MKX-PS4", "Mortal Kombat X on PS4" },
            { "MKX-X", "Mortal Kombat X on Xbox One" },
            { "SFV", "Street Fighter V" },
            { "SG", "Skullgirls on PC" },
            { "ST", "Super Street Fighter 2 Turbo on Fightcade" },
            { "SVC", "SNK vs Capcom Chaos on Fightcade" },
            { "T7-PC", "Tekken 7 on PC" },
            { "T7-PS4", "Tekken 7 on PS4" },
            { "T7-X", "Tekken 7 on Xbox One" },
            { "SF4-PC", "Ultra Street Fighter IV on PC" },
            { "SF4-PS4", "Ultra Street Fighter IV on PS4" },
            { "XRD-PC", "Guilty Gear Xrd on PC" },
            { "XRD-PS4", "Guilty Gear Xrd on PS4" },
        };

        private static readonly List<LfgEntry> LfgList = new List<LfgEntry>();

        [Command("lfg"), Summary("Looking for games")]
        public async Task Lfg([Summary("The game")] string game = null, 
            [Summary("Whether or not the bot should mention you when someone is looking for a game")] string command = null)
        {
            var response = "";

            LfgList.RemoveAll(x => x.Expiry < DateTime.Now);

            if (String.IsNullOrWhiteSpace(game))
            {
                response = ListPlayersLookingForGames();
                await Context.User.SendMessageAsync(response);
            }
            else if (game == "?")
            {
                response = ListSupportedGames(game);
                await Context.User.SendMessageAsync($"```{response}```");

            }
            else if (game == "help")
            {
                response = GetHelpMessage();
                await Context.User.SendMessageAsync(response);
            }
            else
            {   
                response = RegisterPlayer(Context.User, game, (command ?? "").Trim());
                await ReplyAsync(response);
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
                "" + Environment.NewLine + 
                "Notes:" + Environment.NewLine +
                "- Do not include the square brackets ([]) when specifying the game" + Environment.NewLine +
                "- Player registration for a game expires after 2 hours by default" + Environment.NewLine +
                "- When auto-mention is enabled, you will only get mentioned once every 10 minutes for all games" + Environment.NewLine +
                "- The value in square brackets next to the users name indicates when their request for games expires" + Environment.NewLine;

            return $"```{response}```";
        }

        private string RegisterPlayer(IUser user, string game, string command)
        {
            var description = "";
            if (!_games.TryGetValue(game, out description))
            {
                return $"Game {game} is not supported.  Use the \"lfg ?\" command to list supported games";
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

            response += ListPlayersLookingForGames(game, true, true);
            return response;

        }

        private string ListPlayersLookingForGames(string game = null, bool excludeCurrentUser = false, bool enableMentions = false)
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
                response = $"Noone{extra}is looking for games right now";
            }

            return response;
        }

        private string ListSupportedGames(string game)
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

    // Create a module with the 'sample' prefix
    [Group("sample")]
    public class Sample : ModuleBase
    {
        // ~sample square 20 -> 400
        [Command("square"), Summary("Squares a number.")]
        public async Task Square([Summary("The number to square.")] int num)
        {
            // We can also access the channel from the Command Context.
            await Context.Channel.SendMessageAsync($"{num}^2 = {Math.Pow(num, 2)}");
        }

        // ~sample userinfo --> foxbot#0282
        // ~sample userinfo @Khionu --> Khionu#8708
        // ~sample userinfo Khionu#8708 --> Khionu#8708
        // ~sample userinfo Khionu --> Khionu#8708
        // ~sample userinfo 96642168176807936 --> Khionu#8708
        // ~sample whois 96642168176807936 --> Khionu#8708
        [Command("userinfo"), Summary("Returns info about the current user, or the user parameter, if one passed.")]
        [Alias("user", "whois")]
        public async Task UserInfo([Summary("The (optional) user to get info for")] IUser user = null)
        {
            var userInfo = user ?? Context.Client.CurrentUser;
            await ReplyAsync($"{userInfo.Username}#{userInfo.Discriminator}");
        }

    }
}