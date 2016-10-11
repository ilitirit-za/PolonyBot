using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Discord;
using Discord.Commands;
using log4net;
using Polony.Dal;
using Polony.Domain.Danisen.Model;
using RestSharp.Extensions;

namespace Polony
{
    public class PolonyBot : IDisposable
    {
        private readonly DiscordClient _client = new DiscordClient();

        private DanisenDao _dao;
        private string _botToken;
        private readonly ILog _logger;
        private readonly ulong _serverId;
        private readonly ulong _danisenChannelId;
        private Server _server;

        private readonly char _defaultPrefix;

        public PolonyBot(ILog logger)
        {
            _logger = logger;

            var config = ConfigurationManager.OpenExeConfiguration(Assembly.GetExecutingAssembly().Location);

            _serverId = Convert.ToUInt64(config.AppSettings.Settings["ServerId"].Value);
            _danisenChannelId = Convert.ToUInt64(config.AppSettings.Settings["DanisenChannelId"].Value);
            _defaultPrefix = config.AppSettings.Settings["DefaultPrefix"].Value[0];
            _botToken = config.AppSettings.Settings["BotToken"].Value;
            
            var dbConnectionString = config.ConnectionStrings.ConnectionStrings["DbConnectionString"].ConnectionString;
            // Inject this
            var daoLogger = LogManager.GetLogger(typeof(DanisenDao));
            _dao = new DanisenDao(daoLogger, dbConnectionString);

            
            Initialize();
        }

        private readonly CancellationToken _taskCancellationToken = new CancellationToken();
        public async Task Initialize()
        {
            
            InitCommandService();

            _logger.Info("PolonyBot initialised");

            await PeriodicTask.Run(async () => await DisplayScores(), new TimeSpan(12, 0, 0), _taskCancellationToken);
        }

        private void InitCommandService()
        {
            _logger.Info("Creating command service...");
            var commandService = new CommandService(new CommandServiceConfigBuilder
            {
                PrefixChar = _defaultPrefix,
                AllowMentionPrefix = false,
                CustomPrefixHandler = m => 0,
                HelpMode = HelpMode.Disabled,
                ErrorHandler = async (s, e) =>
                {
                    if (e.ErrorType != CommandErrorType.BadPermissions)
                        return;

                    if (string.IsNullOrWhiteSpace(e.Exception?.Message))
                        return;

                    try
                    {
                        await e.Channel.SendMessage(e.Exception.Message).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[{MethodBase.GetCurrentMethod().Name}] {ex.Message}");
                    }
                }
            });

            _logger.Info("Adding basic commands...");
            commandService.CreateCommand("games")
                .Do(async (e) =>
                {
                    await DisplayGames(e);
                });

            commandService.CreateCommand("help")
                .Alias("h")
                .Do(async (e) =>
                {
                    await DisplayHelpText(e);
                });

            commandService.CreateCommand("pb")
                .Parameter("command", ParameterType.Optional)
                .Parameter("parameter1", ParameterType.Optional)
                .Do(async (e) =>
                {
                    var command = e.GetArg("command");
                    if (String.IsNullOrWhiteSpace(command))
                    {
                        await DisplayLeagues(e);
                    }
                    else
                    {
                        if (command.ToLower() == "ranks")
                        {
                            await DisplayRanks(e);
                        }
                        else if (command.ToLower() == "players")
                        {
                            await DisplayRegisteredPlayers(e);
                        }
                        else if (command.ToLower() == "clearcache" || command.ToLower() == "cc")
                        {
                            await ClearMemoryCache(e);
                        }
                        else if (command.ToLower() == "echo" || command.ToLower() == "e")
                        {
                            await EchoText(e);
                        }
                    }
                });

            AddGameCommands(commandService);

            _client.AddService(commandService);
            _client.MessageReceived += _client_MessageReceived;

        }

        private async Task EchoText(CommandEventArgs e)
        {
            var text = e.GetArg("parameter1").ToUpper();
            _logger.Info($"[{e.User.Name}] Echoing text '{text}'");

            if (e.User.HasRole(_server.FindRoles("PolonyAdmin", true).First()))
            {
                await DanisenChannel.SendMessage($"Echo: {text}");
            }
        }

        private async Task DisplayHelpText(CommandEventArgs e)
        {
            var prefix = _defaultPrefix;
            var helpMessage =
                "The following commands are supported by PolonyBot (anything in square brackets is optional):" + Environment.NewLine +
                $"{prefix}h[elp]           - Display this message" + Environment.NewLine +
                $"{prefix}games            - Display a list of registered games" + Environment.NewLine +
                $"{prefix}sfv              - Display a list of players in the SFV league" + Environment.NewLine +
                $"{prefix}sfv r[egister]   - Register for 'SFV'" + Environment.NewLine +
                $"{prefix}sfv v[s]         - Display a list of your recent SFV challenges" + Environment.NewLine +
                $"{prefix}sfv v[s] 45      - Issue an SFV challenge to the player with the Challenge Code 45" + Environment.NewLine +
                $"{prefix}sfv a[ccept] 21  - Accept the challenge with the ID 21" + Environment.NewLine +
                $"{prefix}sfv d[ecline] 21 - Decline the challenge with the ID 21" + Environment.NewLine +
                $"{prefix}sfv w[in] 21     - Declare yourself the winner of the challenge with ID 21" + Environment.NewLine +
                $"{prefix}sfv scores       - Display the high score table for SFV" + Environment.NewLine +
                Environment.NewLine +
                "Note:  The challenge format for SFV is FT3";

            _logger.Info($"Sending help message to {e.User.Name}...");
            await e.User.SendMessage($"```{helpMessage}```");

        }

        private async Task ClearMemoryCache(CommandEventArgs e)
        {
            _logger.Info($"Cache clear requested by {e.User.Name}");

            if (e.User.HasRole(_server.FindRoles("PolonyAdmin", true).First()))
            {
                _dao.ClearCaches();
                await DanisenChannel.SendMessage($"Memory cache cleared!");

                _logger.Info($"Cache cleared");
            }
        }

        private void AddGameCommands(CommandService commandService)
        {
            _logger.Info("Adding game commands...");

            var leagues = _dao.GetAvalailableDanisenLeagues();
            foreach (var league in leagues)
            {
                var gameAlias = league.Game.ShortName;
                if (commandService.AllCommands.Any(c => c.Text == gameAlias))
                    continue;

                commandService.CreateCommand(gameAlias)
                .Parameter("command", ParameterType.Optional)
                .Parameter("parameter1", ParameterType.Optional)
                .Parameter("parameter2", ParameterType.Optional)
                .Parameter("parameter3", ParameterType.Optional)
                .Do(async (e) =>
                {
                    var command = e.GetArg("command");
                    if (String.IsNullOrWhiteSpace(command))
                    {
                        await DisplayRegisteredPlayers(e);
                    }
                    else
                    {
                        if (command.ToLower() == "register" || command.ToLower() == "r")
                        {
                            await RegisterPlayer(e);
                        }
                        else if (command.ToLower() == "ranks")
                        {
                            await DisplayRanks(e);
                        }
                        else if (command.ToLower() == "vs" || command.ToLower() == "v")
                        {
                            await IssueChallenge(e);
                        }
                        else if (command.ToLower() == "accept" || command.ToLower() == "a")
                        {
                            await AcceptChallenge(e);
                        }
                        else if (command.ToLower() == "decline" || command.ToLower() == "d")
                        {
                            await RejectChallenge(e);
                        }
                        else if (command.ToLower() == "win" || command.ToLower() == "w")
                        {
                            await ReportVictory(e);
                        }
                        else if (command.ToLower() == "scores")
                        {
                            await DisplayScores(e);
                        }
                    }
                });
            }
        }

        private async Task ReportVictory(CommandEventArgs e)
        {
            var challengeIdString = e.GetArg("parameter1");
            var challengeId = 0;
            if (!Int32.TryParse(challengeIdString, out challengeId))
            {
                await e.User.SendMessage("Invalid challenge ID");
                return;
            }

            var challenge = _dao.GetChallenges().FirstOrDefault(c => c.ChallengeId == challengeId);
            if (challenge == null)
            {
                await e.User.SendMessage($"Challenge with ID {challengeId} not found");
                return;
            }

            if (challenge.ChallengeStatus != "ACCEPTED")
            {
                await e.User.SendMessage($"Challenge with ID {challengeId} has not been ACCEPTED.  The current status is {challenge.ChallengeStatus}");
                return;
            }

            var p1Id = Convert.ToUInt64(challenge.PlayerOne.Player.DiscordUserId);
            var p2Id = Convert.ToUInt64(challenge.PlayerTwo.Player.DiscordUserId);

            Rank playerOneRank, playerTwoRank;
            if (p1Id == e.User.Id)
            {
                _dao.UpdateChallengeWinner(challengeId, challenge.PlayerOne.RegistrationCode);
                playerOneRank = _dao.IncrementPlayerPoints(challenge.PlayerOne.RegistrationCode);
                playerTwoRank = _dao.DecrementPlayerPoints(challenge.PlayerTwo.RegistrationCode);
                await DanisenChannel.SendMessage($"**{challenge.PlayerOne.Player.Name}** challenged and beat **{challenge.PlayerTwo.Player.Name}** in **{challenge.DanisenLeague.Game.Name}**! :free:");
            }
            else if (p2Id == e.User.Id)
            {
                _dao.UpdateChallengeWinner(challengeId, challenge.PlayerTwo.RegistrationCode);
                playerOneRank = _dao.DecrementPlayerPoints(challenge.PlayerOne.RegistrationCode);
                playerTwoRank = _dao.IncrementPlayerPoints(challenge.PlayerTwo.RegistrationCode);
                await DanisenChannel.SendMessage($"**{challenge.PlayerTwo.Player.Name}** beat the challenger **{challenge.PlayerOne.Player.Name}** in **{challenge.DanisenLeague.Game.Name}**! :free:");
            }
            else
            {
                await e.User.SendMessage("You cannot update a challenge that was not meant for you.");
                return;
            }

            await e.User.SendMessage("Victory recorded! :trophy:");

            if (playerOneRank.Level > challenge.PlayerOne.Rank.Level)
            {
                await DanisenChannel.SendMessage($"**{challenge.PlayerOne.Player.Name}** was promoted to **{playerOneRank.Name}** in **{challenge.DanisenLeague.Game.Name}**! :thumbsup:");
            }
            else if (playerOneRank.Level < challenge.PlayerOne.Rank.Level)
            {
                await DanisenChannel.SendMessage($"**{challenge.PlayerOne.Player.Name}** was demoted to **{playerOneRank.Name}** in **{challenge.DanisenLeague.Game.Name}**! :thumbsdown:");
            }

            if (playerTwoRank.Level > challenge.PlayerTwo.Rank.Level)
            {
                await
                    DanisenChannel.SendMessage(
                        $"**{challenge.PlayerTwo.Player.Name}** was promoted to **{playerTwoRank.Name}** in **{challenge.DanisenLeague.Game.Name}**! :thumbsup:");
            }
            else if (playerTwoRank.Level < challenge.PlayerTwo.Rank.Level)
            {
                await
                    DanisenChannel.SendMessage(
                        $"**{challenge.PlayerTwo.Player.Name}** was demoted to **{playerTwoRank.Name}** in **{challenge.DanisenLeague.Game.Name}**! :thumbsdown:");
            }

            _dao.ClearCaches();
        }
        private async Task AcceptChallenge(CommandEventArgs e)
        {
            var challengeIdString = e.GetArg("parameter1");
            var challengeId = 0;
            if (!Int32.TryParse(challengeIdString, out challengeId))
            {
                await e.User.SendMessage("Invalid challenge ID");
                return;
            }

            var challenge = _dao.GetChallenges().FirstOrDefault(c => c.ChallengeId == challengeId);
            if (challenge == null)
            {
                await e.User.SendMessage($"Challenge with ID {challengeId} not found");
                return;
            }

            var p2Id = Convert.ToUInt64(challenge.PlayerTwo.Player.DiscordUserId);

            if (p2Id != e.User.Id)
            {
                await e.User.SendMessage("You cannot accept a challenge that was not meant for you.");
                return;
            }

            try
            {
                _dao.AcceptChallenge(challengeId);

                var playerOneName = challenge.PlayerOne.Player.Name;
                var playerTwoName = challenge.PlayerTwo.Player.Name;

                await
                    DanisenChannel.SendMessage($"**{playerTwoName}** has accepted **{playerOneName}**'s **{challenge.DanisenLeague.Game.Name}** challenge! :punch:");

                var p1Id = Convert.ToUInt64(challenge.PlayerOne.Player.DiscordUserId);
                var challenger = _server.Users.First(u => u.Id == p1Id);

                var gameAlias = challenge.DanisenLeague.Game.ShortName;
                await challenger.SendMessage(
                    $"**{playerTwoName}** has accepted your **{challenge.DanisenLeague.Game.Name}** challenge!" +
                    Environment.NewLine +
                    $"After you've destroyed your opponent type **${gameAlias} WIN {challengeId}** to record your victory!");

                await e.User.SendMessage($"Challenge accepted!  After you've destroyed your opponent type **${gameAlias} WIN {challengeId}** to record your victory!");
            }
            catch (Exception ex)
            {
                await e.User.SendMessage($"Could not accept challenge.  Speak to a mod about this. ({ex.Message})");
                return;
            }


        }
        private async Task RejectChallenge(CommandEventArgs e)
        {
            var challengeIdString = e.GetArg("parameter1");
            var challengeId = 0;
            if (!Int32.TryParse(challengeIdString, out challengeId))
            {
                await e.User.SendMessage("Invalid challenge ID");
                return;
            }

            var challenge = _dao.GetChallenges().FirstOrDefault(c => c.ChallengeId == challengeId);
            if (challenge == null)
            {
                await e.User.SendMessage($"Challenge with ID {challengeId} not found");
                return;
            }

            var p2Id = Convert.ToUInt64(challenge.PlayerTwo.Player.DiscordUserId);
            if (p2Id != e.User.Id)
            {
                await e.User.SendMessage("You cannot accept a challenge that was not meant for you.");
                return;
            }

            try
            {
                _dao.RejectChallenge(challengeId);

                var playerOneName = challenge.PlayerOne.Player.Name;
                var playerTwoName = challenge.PlayerTwo.Player.Name;

                await
                    DanisenChannel.SendMessage($"**{playerTwoName}** has declined **{playerOneName}**'s **{challenge.DanisenLeague.Game.Name}** challenge! :chicken:");

                var p1Id = Convert.ToUInt64(challenge.PlayerOne.Player.DiscordUserId);

                var challenger = _server.Users.First(u => u.Id == p1Id);

                await challenger.SendMessage(
                    $"**{playerTwoName}** has **DECLINED** your **{challenge.DanisenLeague.Game.Name}** challenge! ({challengeId})");

                await e.User.SendMessage("Challenge declined!");
            }
            catch (Exception ex)
            {
                await e.User.SendMessage($"Could not decline challenge.  Speak to a mod about this. ({ex.Message})");
                return;
            }


        }
        private async Task IssueChallenge(CommandEventArgs e)
        {
            var gameAlias = e.Command.Text.ToUpper();

            var playerCode = e.GetArg("parameter1").ToUpper();
            var opponentCode = e.GetArg("parameter2").ToUpper();

            // In multichar registrations, you don't need to specify
            // your own code
            if (String.IsNullOrWhiteSpace(opponentCode))
                opponentCode = playerCode;

            if (String.IsNullOrWhiteSpace(opponentCode))
            {
                var recentChallenges = _dao.GetChallenges(e.User.Id.ToString(), gameAlias);
                var message = "Recent challenges:" + Environment.NewLine;
                foreach (var challenge in recentChallenges)
                {
                    message +=
                        $"{challenge.PlayerOne.Player.Name} [{challenge.PlayerOne.Character}] vs {challenge.PlayerTwo.Player.Name} [{challenge.PlayerTwo.Character}] " +
                        $"in {challenge.DanisenLeague.Game.ShortName}: {challenge.ChallengeStatus}{Environment.NewLine}" ;
                }

                await e.User.SendMessage($"```{message}```");
                return;
            }

            var league =
                _dao.GetAvalailableDanisenLeagues()
                    .FirstOrDefault(l => l.Game.ShortName.Equals(gameAlias, StringComparison.CurrentCultureIgnoreCase));

            if (league == null)
            {
                await e.User.SendMessage($"Sorry, there is no {gameAlias} Danisen League.");
                return;
            }

            int numericPlayerCode = 0;
            if (!Int32.TryParse(playerCode, out numericPlayerCode))
            {
                await e.User.SendMessage($"**{playerCode}** is not a valid player code!");
                return;
            }

            var playerOne = playerCode.Equals(opponentCode)
                ? _dao.GetRegisteredPlayers(gameAlias).FirstOrDefault(p => p.Player.DiscordUserId.Equals(e.User.Id.ToString()))
                : _dao.GetRegisteredPlayers(gameAlias).FirstOrDefault(p => p.Player.DiscordUserId.Equals(e.User.Id.ToString()) && p.RegistrationCode == numericPlayerCode);

            if (playerOne == null)
            {
                await e.User.SendMessage($"You are not registered for the {gameAlias} League.");
                return;
            }

            int numericOpponentCode = 0;
            if (!Int32.TryParse(opponentCode, out numericOpponentCode))
            {
                await e.User.SendMessage($"**{opponentCode}** is not a valid player code!");
                return;
            }

            var playerTwo = _dao.GetRegisteredPlayers(gameAlias)
                    .FirstOrDefault(p => p.RegistrationCode == numericOpponentCode && p.DanisenLeague.Game.ShortName.Equals(gameAlias, StringComparison.CurrentCultureIgnoreCase));

            if (playerTwo == null)
            {
                await e.User.SendMessage($"No player with the code {opponentCode} is registered in the {gameAlias} League.");
                return;
            }

            if (playerTwo.Player.DiscordUserId == playerOne.Player.DiscordUserId)
            {
                await e.User.SendMessage($"You cannot challenge yourself!");
                return;
            }

            // Check if there is an outstanding challenge:
            var challenges = _dao.GetChallenges(playerOne.Player.DiscordUserId, gameAlias);
            var openChallenges = challenges.Where(c =>
                        (c.PlayerOne.RegistrationCode == numericOpponentCode ||
                         c.PlayerTwo.RegistrationCode == numericOpponentCode)
                        && c.ChallengeStatus != "COMPLETE" && c.ChallengeStatus != "REJECTED" &&
                        c.ChallengeStatus != "EXPIRED").ToList();


            if (openChallenges.Any())
            {
                var message = $"You already have an open challenge against {playerTwo.Player.Name}:";
                var openChallenge = openChallenges.First();
                message += Environment.NewLine + $"ChallengeId: {openChallenge.ChallengeId}. " + Environment.NewLine +
                    $"Issued by {openChallenge.PlayerOne.Player.Name} on {openChallenge.ChallengeIssued.ToString("yyyy-MM-dd HH:mm")}. " + Environment.NewLine +
                    $"Status: {InterpretChallengeStatus(openChallenge)}";

                await e.User.SendMessage($"```{message}```");

                return;
            }

            // Check if player is allowed to challenge opponent
            if (playerOne.Rank.LowerChallengeLimit > playerTwo.Rank.Level || 
                playerOne.Rank.Level < playerTwo.Rank.LowerChallengeLimit ||
                playerOne.Rank.UpperChallengeLimit < playerTwo.Rank.Level
                )
            {
                var message = $"You can only challenge players who are Ranked between levels {playerOne.Rank.LowerChallengeLimit} and {playerOne.Rank.UpperChallengeLimit}.";
                
                await e.User.SendMessage($"```{message}```");

                return;
            }

            var challengeId = 0;
            _dao.IssueChallenge(playerOne, playerTwo, out challengeId);
            await e.User.SendMessage($"Challenge issued!  Your Challenge ID is {challengeId}");
            var p2Id = Convert.ToUInt64(playerTwo.Player.DiscordUserId);
            var opponent = _server.Users.First(u => u.Id == p2Id);

            var challengeDetails = $"You have been challenged by **{playerOne.Player.Name}** ({playerOne.Rank.Name})!" + Environment.NewLine +
                                   "```" +
                                   $"Game: {playerOne.DanisenLeague.Game.Name}" + Environment.NewLine +
                                   $"ChallengeId: {challengeId}" + Environment.NewLine;
            if (!league.MultipleCharactersAllowed)
                challengeDetails +=
                    $"Matchup: {playerOne.Player.Name} **{playerOne.Character}** vs You **{playerTwo.Character}** ";

            challengeDetails += "```" + Environment.NewLine;

            challengeDetails +=
                $"Type **${gameAlias} ACCEPT {challengeId}** to accept the challenge OR **${gameAlias} DECLINE {challengeId}** to reject it.";

            await opponent.SendMessage(challengeDetails);

            if (league.MultipleCharactersAllowed)
                await DanisenChannel.SendMessage($"**{playerOne.Player.Name}** challenged **{playerTwo.Player.Name}** to a **{gameAlias}** match!");
            else
                await DanisenChannel.SendMessage($"**{playerOne.Player.Name}** challenged **{playerTwo.Player.Name}** " +
                                            $"to a **{playerOne.Character}** vs **{playerTwo.Character}** match in **{gameAlias}**!");
        }
        private string InterpretChallengeStatus(DanisenChallenge challenge)
        {
            if (challenge.ChallengeStatus == "ISSUED")
            {
                return $"Waiting for {challenge.PlayerTwo.Player.Name} to Accept";
            }
            else if (challenge.ChallengeStatus == "ACCEPTED")
            {
                return "Waiting for result.";
            }

            return challenge.ChallengeStatus;
        }
        private async Task RegisterPlayer(CommandEventArgs e)
        {
            var gameAlias = e.Command.Text.ToUpper();
            var character = e.GetArg("parameter1").ToUpper();
            var challengeInt = 0;
            var returnCode = _dao.RegisterPlayerForLeague(e.User.Id.ToString(), gameAlias, ref character, out challengeInt);

            var challengeCode = challengeInt.ToString().PadLeft(5, '0');

            switch (returnCode)
            {
                case DanisenDao.DaoReturnCode.Success:
                    await e.User.SendMessage($"Registration successful.  Your challenge code is {challengeCode}");
                    if (character.Equals("ANY", StringComparison.CurrentCultureIgnoreCase))
                    {
                        await DanisenChannel.SendMessage($"**{e.User.Name}** registered for the **{gameAlias}** League.  Challenge Code: **{challengeCode}**");
                    }
                    else
                        await DanisenChannel.SendMessage($"**{e.User.Name}** registered **{character}** for the **{gameAlias}** League.  Challenge Code: **{challengeCode}**");
                    break;

                case DanisenDao.DaoReturnCode.GameDoesNotExist:
                    await e.User.SendMessage($"The game **{gameAlias.ToUpper()}** is not registered");
                    break;

                case DanisenDao.DaoReturnCode.LeagueDoesNotExist:
                    await e.User.SendMessage($"There is currently no active league for **{gameAlias}**");
                    break;

                case DanisenDao.DaoReturnCode.RankDoesNotExist:
                    await e.User.SendMessage($"There is no beginner rank.  Please consult a moderator.");
                    break;

                case DanisenDao.DaoReturnCode.CharacterRequired:
                    await e.User.SendMessage($"You have to specify a character to enter the {gameAlias} league");
                    break;

                default:
                    break;
            }
        }
        private async Task DisplayRegisteredPlayers(CommandEventArgs e)
        {
            var gameAlias = e.GetArg("parameter1");

            if (e.Command.Text != "danisen")
                gameAlias = e.Command.Text;

            var registeredPlayers = _dao.GetRegisteredPlayers(gameAlias);
            if (registeredPlayers.Count == 0)
            {
                await e.User.SendMessage($"There are no players registered for the **{gameAlias.ToUpper()}** league");
            }
            else
            {
                var message = $"Registered Danisen players for {gameAlias.ToUpper()}:{Environment.NewLine}";
                foreach (var registration in registeredPlayers)
                {
                    var character = "";
                    if (!registration.DanisenLeague.MultipleCharactersAllowed)
                        character = $"[{registration.Character}]";

                    message += $"{registration.Player.Name.PadRight(15)}{character.PadRight(10)} " +
                               $"Rank: {registration.Rank.Level} - {registration.Rank.Name.PadRight(15)}({registration.RegistrationCode.ToString().PadLeft(5, '0')})" +
                               Environment.NewLine;
                }

                await e.User.SendMessage($"```{message}```");
            }
        }
        private async Task DisplayGames(CommandEventArgs e)
        {
            var games = _dao.GetGames();

            if (games.Count == 0)
            {
                await e.User.SendMessage("**Sorry, I don't have any games registered**");
            }
            else
            {
                var message = games.Aggregate("The following games are registered:",
                    (current, game) => current + $"{Environment.NewLine} {game.Name} [{game.ShortName}]");

                await e.User.SendMessage($"```{message}```");
            }
        }
        private async Task DisplayScores(CommandEventArgs e = null)
        {
            var scores = _dao.GetScores();

            if (e != null)
            {
                var gameAlias = e.Command.Text;

                scores = scores.Where(s => s.GameName.Equals(gameAlias, StringComparison.CurrentCultureIgnoreCase)).ToList();
            }

            var message = "Player".PadRight(20)
                + "Game".PadRight(7) 
                + "Char".PadRight(10)
                + "Rank".PadRight(15) 
                + "Played".PadRight(8) 
                + "Won".PadRight(4) 
                + "Win %".PadLeft(6)
                + "LP".PadLeft(7)
                + Environment.NewLine;
            message = scores.Aggregate(message, 
                (current, score) => current + 
                    
                    score.PlayerName.PadRight(20) +
                    score.GameName.PadRight(7) +
                    score.Character.PadRight(10) +
                    score.Rank.PadRight(15) + 
                    score.Played.ToString().PadLeft(6) + 
                    score.Won.ToString().PadLeft(5) + 
                    score.WinRate.ToString("0.00").PadLeft(7) +
                    score.LeaguePoints.ToString().PadLeft(7) +
                    Environment.NewLine
                );


            if (e != null)
            {
                await e.User.SendMessage($"```{message}```");
            }
            else
            {
                await DanisenChannel.SendMessage($"```{message}```");
            }
            
            
        }

        private async Task DisplayLeagues(CommandEventArgs e)
        {
            var leagues = _dao.GetAvalailableDanisenLeagues();

            if (leagues.Count == 0)
            {
                await e.User.SendMessage("Sorry, I don't have any leagues registered");
            }
            else
            {
                var message = leagues.Aggregate("The following leagues are available:",
                    (current, league) => current + $"{Environment.NewLine} {league.DanisenLeagueId} - {league.Game.Name} - " +
                                         $"{(league.MultipleCharactersAllowed ? "Multi Character" : "Single Character")}");

                await e.User.SendMessage($"```{message}```");
            }
        }

        private async Task DisplayRanks(CommandEventArgs e)
        {
            var gameAlias = e.Command.Text;

            var league = _dao.GetAvalailableDanisenLeagues().First(l => l.Game.ShortName.Equals(gameAlias,
                StringComparison.CurrentCultureIgnoreCase));
            
            var ranks = _dao.GetRanks(league.RankSetId);

            if (ranks.Count == 0)
            {
                await e.User.SendMessage("Sorry, I don't have any ranks registered");
            }
            else
            {
                var message = ranks.Aggregate("The following ranks are available:",
                    (current, rank) =>
                        current + $"{Environment.NewLine} {rank.Level}. {(rank.Unlocked ? rank.Name : "?????????")}");

                await e.User.SendMessage($"```{message}```");
            }
        }

        public async void Connect()
        {
            _logger.Info("Attempting to connect...");

            await _client.Connect(_botToken, TokenType.Bot);

            // Wait till the server becomes available
            await Task.Delay(2000);

            _server = _client.GetServer(_serverId);
            
            _logger.Info("PolonyBot connected");
        }

        public async void Disconnect()
        {
            _logger.Info("PolonyBot disconnecting...");
            await _client.Disconnect();
            _logger.Info("PolonyBot disconnected");
        }

        private void _client_MessageReceived(object sender, MessageEventArgs e)
        {
            if (!e.User.IsBot)
                _dao.AddPlayer(new Player { DiscordUserId = e.User.Id.ToString(), Name = e.User.Name });
        }

        private Channel _danisenChannel;

        private Channel DanisenChannel
        {
            get
            {
                return _danisenChannel ??
                    (_danisenChannel = _server.GetChannel(_danisenChannelId));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dao?.Dispose();
                _dao = null;
            }
        }
    }
}