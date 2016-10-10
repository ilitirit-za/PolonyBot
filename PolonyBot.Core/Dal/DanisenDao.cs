using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using log4net;
using Polony.Domain.Danisen.Model;

namespace Polony.Dal
{
    public class DanisenDao : IDisposable
    {
        private MemoryCache _cache = MemoryCache.Default;
        private DbConnection _dbConnection;
        private readonly ILog _logger;

        public DanisenDao(ILog logger, string connectionString)
        {
            _logger = logger;
            var factory = DbProviderFactories.GetFactory("System.Data.SqlClient");
            _dbConnection = factory.CreateConnection();
            _dbConnection.ConnectionString = connectionString;
        }

        private void EnsureConnection()
        {
            if (_dbConnection.State != ConnectionState.Open)
                _dbConnection.Open();
        }

        private void CloseConnection()
        {
            _dbConnection.Close();
        }

        public void ClearCaches()
        {
            _cache?.Dispose();
            _cache = MemoryCache.Default;
        }

        public List<Game> GetGames()
        {
            var gameList = _cache["Games"] as List<Game>;

            if (gameList == null)
            {
                using (var command = _dbConnection.CreateCommand())
                {
                    command.CommandText = "SELECT GameId, Name, ShortName FROM Game";
                    command.CommandType = CommandType.Text;

                    try
                    {
                        EnsureConnection();

                        using (var reader = command.ExecuteReader())
                        {
                            gameList = new List<Game>();

                            while (reader.Read())
                            {
                                var gameId = reader.GetFieldValue<int>(0);
                                var name = reader.GetFieldValue<string>(1);
                                var shortName = reader.GetFieldValue<string>(2);

                                gameList.Add(new Game(gameId, name, shortName));
                            }

                            var policy = new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddHours(12) };
                            _cache.Add("Games", gameList, policy);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Error($"[{MethodBase.GetCurrentMethod().Name}] {e.Message}");
                    }
                    finally
                    {
                        CloseConnection();
                    }
                }
            }

            return gameList;
        }

        public List<DanisenRegistration> GetRegisteredPlayers(string gameAlias)
        {
            var registeredPlayers = GetRegisteredPlayers();
            var filteredList = registeredPlayers.Where(
                p => p.DanisenLeague.Game.ShortName.Equals(gameAlias, StringComparison.CurrentCultureIgnoreCase));

            return filteredList.ToList();
        }

        public List<DanisenRegistration> GetRegisteredPlayers()
        {
            var list = _cache["RegisteredPlayers"] as List<DanisenRegistration>;

            if (list == null)
            {
                using (var command = _dbConnection.CreateCommand())
                {
                    command.CommandText = @"SELECT [G].[GameId]
, [G].[Name]
, [G].[ShortName]
, [P].[PlayerId]
, [P].[DiscordUserId]
, [P].[Name]
, [DL].[DanisenLeagueId]
, [DL].[MultipleCharactersAllowed]
, [DL].[Enabled]
, [DR].[Character]
, [DR].[Points]
, [R].[RankId]
, [R].[Name]
, [R].[Level]
, [R].[PromotionScore]
, [R].[DemotionScore]
, [R].[UpperChallengeLimit]
, [R].[LowerChallengeLimit]
, [R].[Unlocked]
, [DR].RegistrationCode
, [DL].RankSetId
FROM DanisenRegistration DR
JOIN [dbo].[DanisenLeague] DL
    ON [DL].[DanisenLeagueId] = [DR].[DanisenLeagueId]
JOIN [dbo].[Game] G
    ON [G].[GameId] = [DL].[GameId]
JOIN [dbo].[Player] P
    ON [P].[PlayerId] = [DR].[PlayerId]
JOIN [dbo].[Rank] R
    ON [R].RankId = [DR].RankId
ORDER BY [R].Level DESC, [P].Name";

                    command.CommandType = CommandType.Text;

                    try
                    {
                        EnsureConnection();
                        using (var reader = command.ExecuteReader())
                        {
                            list = new List<DanisenRegistration>();

                            while (reader.Read())
                            {
                                var gameId = reader.GetFieldValue<int>(0);
                                var name = reader.GetFieldValue<string>(1);
                                var shortName = reader.GetFieldValue<string>(2);
                                var leagueId = reader.GetFieldValue<int>(6);
                                var league = new DanisenLeague
                                {
                                    DanisenLeagueId = leagueId,
                                    Game = new Game(gameId, name, shortName),
                                    MultipleCharactersAllowed = reader.GetFieldValue<bool>(7),
                                    RankSetId = reader.GetFieldValue<int>(20)
                                };

                                var player = new Player
                                {
                                    PlayerId = reader.GetFieldValue<int>(3),
                                    DiscordUserId = reader.GetFieldValue<string>(4),
                                    Name = reader.GetFieldValue<string>(5),
                                };

                                var characterValue = reader[9];
                                var character = "ANY";

                                if (!(characterValue is DBNull))
                                    character = characterValue.ToString();

                                var rank = new Rank
                                {
                                    RankId = reader.GetFieldValue<int>(11),
                                    RankSetId = reader.GetFieldValue<int>(20),
                                    Name = reader.GetFieldValue<string>(12),
                                    Level = reader.GetFieldValue<int>(13),
                                    PromotionScore = reader.GetFieldValue<int>(14),
                                    DemotionScore = reader.GetFieldValue<int>(15),
                                    UpperChallengeLimit = reader.GetFieldValue<int>(16),
                                    LowerChallengeLimit = reader.GetFieldValue<int>(17),
                                    Unlocked = reader.GetFieldValue<bool>(18),
                                };

                                list.Add(new DanisenRegistration
                                {
                                    DanisenLeague = league,
                                    Player = player,
                                    Rank = rank,
                                    Enabled = reader.GetFieldValue<bool>(8),
                                    Character = character,
                                    Points = reader.GetFieldValue<int>(10),
                                    RegistrationCode = reader.GetFieldValue<int>(19),
                                });
                            }

                            var policy = new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddHours(12) };
                            _cache.Add("RegisteredPlayers", list, policy);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Error($"[{MethodBase.GetCurrentMethod().Name}] {e.Message}");
                    }
                    finally
                    {
                        CloseConnection();
                    }
                }
            }

            return list;
        }

        public List<DanisenLeague> GetAvalailableDanisenLeagues()
        {
            var leagueList = _cache["LeagueList"] as List<DanisenLeague>;

            if (leagueList == null)
            {
                using (var command = _dbConnection.CreateCommand())
                {
                    command.CommandText = @"SELECT G.GameId
, G.Name
, G.ShortName
, DL.DanisenLeagueId
, DL.MultipleCharactersAllowed
, DL.RankSetId
FROM DanisenLeague DL
JOIN Game G
    ON DL.GameId = G.GameId
WHERE DL.Enabled = 1";

                    command.CommandType = CommandType.Text;

                    try
                    {
                        EnsureConnection();
                        using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
                        {
                            leagueList = new List<DanisenLeague>();

                            while (reader.Read())
                            {
                                var gameId = reader.GetFieldValue<int>(0);
                                var name = reader.GetFieldValue<string>(1);
                                var shortName = reader.GetFieldValue<string>(2);
                                var leagueId = reader.GetFieldValue<int>(3);

                                leagueList.Add(new DanisenLeague
                                {
                                    DanisenLeagueId = leagueId,
                                    Game = new Game(gameId, name, shortName),
                                    MultipleCharactersAllowed = reader.GetFieldValue<bool>(4),
                                    RankSetId = reader.GetFieldValue<int>(5),
                                });
                            }

                            var policy = new CacheItemPolicy {AbsoluteExpiration = DateTimeOffset.Now.AddHours(12)};
                            _cache.Add("LeagueList", leagueList, policy);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Error($"[{MethodBase.GetCurrentMethod().Name}] {e.Message}");
                    }
                    finally
                    {
                        CloseConnection();
                    }
                    
                }
            }

            return leagueList;
        }

        public void AddPlayer(Player player)
        {
            using (var command = _dbConnection.CreateCommand())
            {
                command.CommandText =
                    @"INSERT INTO[dbo].[Player]([DiscordUserId], [Name])
SELECT @DiscordUserId, @Name
WHERE NOT EXISTS
(
    SELECT 1
    FROM[dbo].[Player] P
    WHERE [P].[DiscordUserId] = @DiscordUserId
)";
                command.CommandType = CommandType.Text;
                var idParm = command.CreateParameter();
                idParm.DbType = DbType.AnsiString;
                idParm.ParameterName = "DiscordUserId";
                idParm.Value = player.DiscordUserId;
                command.Parameters.Add(idParm);

                var nameParm = command.CreateParameter();
                nameParm.DbType = DbType.AnsiString;
                nameParm.ParameterName = "Name";
                nameParm.Value = player.Name;
                command.Parameters.Add(nameParm);

                try
                {
                    EnsureConnection();
                    command.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    _logger.Error($"[{MethodBase.GetCurrentMethod().Name}] {e.Message}");
                }
                finally
                {
                    CloseConnection();
                }

            }

            if (_cache.Contains("Players"))
                _cache.Remove("Players");
        }

        public List<Rank> GetRanks(int rankSetId)
        {
            var list = _cache[$"RankList{rankSetId}"] as List<Rank>;
            if (!_cache.Contains($"RankList{rankSetId}"))
            {
                using (var command = _dbConnection.CreateCommand())
                {
                    command.CommandText = @"SELECT [RankId]
,[Name]
,[Level]
,[PromotionScore]
,[DemotionScore]
,[UpperChallengeLimit]
,[LowerChallengeLimit]
,[Unlocked]
,[CreatedOn]
FROM [dbo].[Rank]
WHERE [RankSetId] = @rankSetId
ORDER BY [Level]
";

                    command.CommandType = CommandType.Text;
                    var rankSetIdParm = command.CreateParameter();
                    rankSetIdParm.DbType = DbType.Int32;
                    rankSetIdParm.ParameterName = "rankSetId";
                    rankSetIdParm.Value = rankSetId;

                    command.Parameters.Add(rankSetIdParm);

                    try
                    {
                        EnsureConnection();
                        using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
                        {
                            list = new List<Rank>();

                            while (reader.Read())
                            {
                                list.Add(new Rank
                                {
                                    RankId = reader.GetFieldValue<int>(0),
                                    Name = reader.GetFieldValue<string>(1),
                                    Level = reader.GetFieldValue<int>(2),
                                    PromotionScore = reader.GetFieldValue<int>(3),
                                    DemotionScore = reader.GetFieldValue<int>(4),
                                    UpperChallengeLimit = reader.GetFieldValue<int>(5),
                                    LowerChallengeLimit = reader.GetFieldValue<int>(6),
                                    Unlocked = reader.GetFieldValue<bool>(7),
                                });
                            }

                            var policy = new CacheItemPolicy {AbsoluteExpiration = DateTimeOffset.Now.AddHours(12)};

                            _cache.Add($"RankList{rankSetId}", list, policy);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Error($"[{MethodBase.GetCurrentMethod().Name}] {e.Message}");
                    }
                    finally
                    {
                        CloseConnection();
                    }
                }
            }

            return list;
        }

        public DaoReturnCode RegisterPlayerForLeague(string discordUserId, string gameAlias, ref string character,
            out int registrationCode)
        {
            registrationCode = 0;

            _cache.Remove("Players");

            var players = GetPlayers();
            var player = players.FirstOrDefault(
                p => String.Equals(p.DiscordUserId, discordUserId, StringComparison.CurrentCultureIgnoreCase));
            if (player == null)
                return DaoReturnCode.PlayerDoesNotExist;

            var games = GetGames();
            var game =
                games.FirstOrDefault(
                    l => String.Equals(l.ShortName, gameAlias, StringComparison.CurrentCultureIgnoreCase));
            if (game == null)
                return DaoReturnCode.GameDoesNotExist;

            var leagues = GetAvalailableDanisenLeagues();
            var league = leagues.FirstOrDefault(
                l => String.Equals(l.Game.ShortName, gameAlias, StringComparison.CurrentCultureIgnoreCase));
            if (league == null)
                return DaoReturnCode.LeagueDoesNotExist;

            var ranks = GetRanks(league.RankSetId);
            var rank = ranks.FirstOrDefault(r => r.Level == 1);
            if (rank == null)
                return DaoReturnCode.RankDoesNotExist;

            if (String.IsNullOrWhiteSpace(character) && !league.MultipleCharactersAllowed)
                return DaoReturnCode.CharacterRequired;

            if (String.IsNullOrWhiteSpace(character) || league.MultipleCharactersAllowed)
                character = "ANY";

            using (var command = _dbConnection.CreateCommand())
            {
                command.CommandText =
                    @"INSERT INTO [dbo].[DanisenRegistration] ([DanisenLeagueId], [PlayerId], [Character], [RankId])
SELECT @DanisenLeagueId, @PlayerId, @Character, @RankId 
WHERE NOT EXISTS
(
    SELECT 1
    FROM [dbo].[DanisenRegistration] DR
    WHERE [DR].[DanisenLeagueId] = @DanisenLeagueId
    AND [DR].[PlayerId] = @PlayerId
    AND [DR].[Character] = @Character
);

SELECT RegistrationCode 
FROM [dbo].[DanisenRegistration] DR
WHERE [DR].[DanisenLeagueId] = @DanisenLeagueId
AND [DR].[PlayerId] = @PlayerId
AND [DR].[Character] = @Character";

                command.CommandType = CommandType.Text;

                var idParm = command.CreateParameter();
                idParm.DbType = DbType.Int32;
                idParm.ParameterName = "PlayerId";
                idParm.Value = player.PlayerId;
                command.Parameters.Add(idParm);

                var leagueIdParm = command.CreateParameter();
                leagueIdParm.DbType = DbType.Int32;
                leagueIdParm.ParameterName = "DanisenLeagueId";
                leagueIdParm.Value = league.DanisenLeagueId;
                command.Parameters.Add(leagueIdParm);

                var rankIdParm = command.CreateParameter();
                rankIdParm.DbType = DbType.Int32;
                rankIdParm.ParameterName = "RankId";
                rankIdParm.Value = rank.RankId;
                command.Parameters.Add(rankIdParm);

                var characterParm = command.CreateParameter();
                characterParm.DbType = DbType.AnsiString;
                characterParm.ParameterName = "Character";
                characterParm.Value = character;
                command.Parameters.Add(characterParm);

                try
                {
                    EnsureConnection();
                    var ret = command.ExecuteScalar();

                    registrationCode = (int)ret;

                    // Force cache refresh
                    if (_cache.Contains("RegisteredPlayers"))
                        _cache.Remove("RegisteredPlayers");

                    return DaoReturnCode.Success;
                }
                catch (Exception e)
                {
                    _logger.Error($"[{MethodBase.GetCurrentMethod().Name}] {e.Message}");

                    return DaoReturnCode.Failure;
                }
                finally
                {
                    CloseConnection();
                }
            }
        }

        public List<PlayerScore> GetScores()
        {
            using (var command = _dbConnection.CreateCommand())
            {
                command.CommandText =
@"SELECT [PlayerName]
, [GameName]
, [Character]
, [Rank]
, [Played]
, [Won]
, ROUND(Won / CONVERT([float], [Played]) * 100, 2) AS WinRate
, LeaguePoints
FROM
(
    SELECT [P].[Name] AS PlayerName
    , [G].[ShortName] AS GameName
    , [DR].[Character]
    , [R].[Name] [Rank]
    , LP.LeaguePoints
    , COUNT(*) Played
    , (SELECT COUNT(*) FROM [dbo].[DanisenChallenge] DC1 WHERE [DC1].[WinnerChallengeCode] = [DR].[RegistrationCode]) Won
    FROM [dbo].[DanisenChallenge] DC
    JOIN [dbo].[DanisenRegistration] DR
        ON [DR].[DanisenLeagueId] = [DC].[DanisenLeagueId]
    JOIN [dbo].[Player] P
        ON [P].[PlayerId] = [DR].[PlayerId]
    JOIN [dbo].[DanisenLeague] DL
        ON [DL].[DanisenLeagueId] = [DR].[DanisenLeagueId]
    JOIN [dbo].[Game] G
        ON [G].[GameId] = [DL].[GameId]
    JOIN [dbo].[Rank] R
        ON [R].[RankId] = [DR].[RankId]
    JOIN v_LeaguePoints LP
        ON LP.PlayerCode = [DR].[RegistrationCode]
    WHERE [DR].[RegistrationCode] IN ([DC].[P1ChallengeCode], [DC].[P2ChallengeCode])
    AND [WinnerChallengeCode] IS NOT NULL
    GROUP BY [P].[Name], [G].[ShortName], [DR].[RegistrationCode], [DR].[Character], [R].[Name], LP.LeaguePoints
) T
ORDER BY GameName, [Rank], [Played] DESC, [Won] DESC, [PlayerName]";
                command.CommandType = CommandType.Text;

                EnsureConnection();
                using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
                {
                    var list = new List<PlayerScore>();

                    while (reader.Read())
                    {
                        list.Add(new PlayerScore
                        {
                            PlayerName = reader.GetFieldValue<string>(0),
                            GameName = reader.GetFieldValue<string>(1),
                            Character = reader.GetFieldValue<string>(2),
                            Rank = reader.GetFieldValue<string>(3),
                            Played = reader.GetFieldValue<int>(4),
                            Won = reader.GetFieldValue<int>(5),
                            WinRate = Convert.ToDouble(reader.GetFieldValue<double>(6)),
                            LeaguePoints = reader.GetInt32(7)
                        });
                    }

                    // Don't close the connection...
                    return list;
                }
            }
        }

        public List<Player> GetPlayers()
        {
            var list = _cache["Players"] as List<Player>;

            if (list == null)
            {
                using (var command = _dbConnection.CreateCommand())
                {
                    command.CommandText = "SELECT PlayerId, DiscordUserId, Name FROM Player";
                    command.CommandType = CommandType.Text;

                    EnsureConnection();
                    using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
                    {
                        list = new List<Player>();

                        while (reader.Read())
                        {
                            var playerId = reader.GetFieldValue<int>(0);
                            var discordUserId = reader.GetFieldValue<string>(1);
                            var name = reader.GetFieldValue<string>(2);

                            list.Add(new Player
                            {
                                PlayerId = playerId,
                                DiscordUserId = discordUserId,
                                Name = name,
                            });
                        }

                        var policy = new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddHours(12) };
                        _cache.Add("Players", list, policy);
                    }
                }
            }

            return list;
        }

        public void IssueChallenge(DanisenRegistration playerOne, DanisenRegistration playerTwo, out int challengeId)
        {
            // Lots of error checking missing.  We Capcom nao!

            using (var command = _dbConnection.CreateCommand())
            {
                command.CommandText =
                    @"INSERT INTO [dbo].[DanisenChallenge] ([DanisenLeagueId], [P1ChallengeCode], [P2ChallengeCode], [ChallengeStatus])
VALUES (@LeagueId, @P1Code, @P2Code, 'ISSUED'); SELECT CONVERT(INT, SCOPE_IDENTITY());";

                command.CommandType = CommandType.Text;
                var idParm = command.CreateParameter();
                idParm.DbType = DbType.AnsiString;
                idParm.ParameterName = "LeagueId";
                idParm.Value = playerOne.DanisenLeague.DanisenLeagueId;
                command.Parameters.Add(idParm);

                var p1CodeParm = command.CreateParameter();
                p1CodeParm.DbType = DbType.AnsiString;
                p1CodeParm.ParameterName = "P1Code";
                p1CodeParm.Value = playerOne.RegistrationCode;
                command.Parameters.Add(p1CodeParm);

                var p2CodeParm = command.CreateParameter();
                p2CodeParm.DbType = DbType.AnsiString;
                p2CodeParm.ParameterName = "P2Code";
                p2CodeParm.Value = playerTwo.RegistrationCode;
                command.Parameters.Add(p2CodeParm);

                try
                {
                    EnsureConnection();
                    var ret = command.ExecuteScalar();
                    challengeId = (int)ret;
                }
                catch (Exception e)
                {
                    _logger.Error($"[{MethodBase.GetCurrentMethod().Name}] {e.Message}");

                    challengeId = -1;
                }
                finally
                {
                    CloseConnection();
                }

            }

            if (_cache.Contains("Challenges"))
                _cache.Remove("Challenges");
        }

        public List<DanisenChallenge> GetChallenges(string discordUserId, string gameAlias)
        {
            var challenges = GetChallenges();

            return
                challenges.Where(
                    c => c.DanisenLeague.Game.ShortName.Equals(gameAlias, StringComparison.CurrentCultureIgnoreCase)
                         &&
                         (c.PlayerOne.Player.DiscordUserId == discordUserId ||
                          c.PlayerTwo.Player.DiscordUserId == discordUserId)).ToList();
        }

        public List<DanisenChallenge> GetChallenges(int maxAgeInDays = 3)
        {
            var list = _cache["Challenges"] as List<DanisenChallenge>;
            if (list == null)
            {
                var registeredPlayers = GetRegisteredPlayers();
                var danisenLeagues = GetAvalailableDanisenLeagues();

                try
                {
                    using (var command = _dbConnection.CreateCommand())
                    {
                        command.CommandText = @"SELECT [ChallengeId]
    , [DanisenLeagueId]
    , [P1ChallengeCode]
    , [P2ChallengeCode]
    , [ChallengeStatus]
    , [WinnerChallengeCode]
    , [CreatedOn]
    FROM [dbo].[DanisenChallenge] DC
    WHERE DATEDIFF(DAY, CreatedOn, GETDATE()) < @maxAgeInDays";

                        command.CommandType = CommandType.Text;

                        var ageParm = command.CreateParameter();
                        ageParm.DbType = DbType.Int32;
                        ageParm.ParameterName = "maxAgeInDays";
                        ageParm.Value = maxAgeInDays;
                        command.Parameters.Add(ageParm);

                        list = new List<DanisenChallenge>();

                        EnsureConnection();

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var challengeId = reader.GetFieldValue<int>(0);
                                var leagueId = reader.GetFieldValue<int>(1);
                                var p1Code = reader.GetFieldValue<int>(2);
                                var p2Code = reader.GetFieldValue<int>(3);
                                var status = reader.GetFieldValue<string>(4);
                                var winner = reader[5] is DBNull ? null : (int?)reader[5];
                                var challengeIssued = reader.GetFieldValue<DateTime>(6);


                                var challenge = new DanisenChallenge
                                {
                                    ChallengeId = challengeId,
                                    DanisenLeague = danisenLeagues.First(dl => dl.DanisenLeagueId == leagueId),
                                    PlayerOne = registeredPlayers.First(r => r.RegistrationCode == p1Code),
                                    PlayerTwo = registeredPlayers.First(r => r.RegistrationCode == p2Code),
                                    ChallengeStatus = status,
                                    ChallengeIssued = challengeIssued,
                                };

                                if (winner != null)
                                    challenge.Winner =
                                        registeredPlayers.FirstOrDefault(p => p.RegistrationCode == winner.Value).Player;

                                list.Add(challenge);


                            }
                        }

                        var policy = new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddHours(12) };
                        _cache.Add("Challenges", list, policy);
                    }
                }
                catch (Exception e)
                {
                    _logger.Error($"[{MethodBase.GetCurrentMethod().Name}] {e.Message}");

                    throw;
                }
                finally
                {
                    _dbConnection.Close();
                }
            }

            return list;
        }

        public enum DaoReturnCode
        {
            Failure = -1,
            Success = 0,
            PlayerDoesNotExist,
            GameDoesNotExist,
            LeagueDoesNotExist,
            RankDoesNotExist,
            CharacterRequired,
        }

        public void AcceptChallenge(int challengeId)
        {
            UpdateChallengeStatus(challengeId, "ACCEPTED");
        }

        public void RejectChallenge(int challengeId)
        {
            UpdateChallengeStatus(challengeId, "REJECTED");
        }

        public void UpdateChallengeStatus(int challengeId, string status)
        {
            using (var command = _dbConnection.CreateCommand())
            {
                command.CommandText =
                    @"UPDATE DanisenChallenge SET ChallengeStatus = @status WHERE ChallengeId = @challengeId";

                var statusParm = command.CreateParameter();
                statusParm.DbType = DbType.AnsiString;
                statusParm.ParameterName = "status";
                statusParm.Value = status;
                command.Parameters.Add(statusParm);

                var challengeIdParm = command.CreateParameter();
                challengeIdParm.DbType = DbType.AnsiString;
                challengeIdParm.ParameterName = "challengeId";
                challengeIdParm.Value = challengeId;
                command.Parameters.Add(challengeIdParm);

                try
                {
                    EnsureConnection();
                    command.ExecuteNonQuery();

                    _cache.Remove("Challenges");
                }
                catch (Exception e)
                {
                    _logger.Error($"[{MethodBase.GetCurrentMethod().Name}] {e.Message}");

                }
                finally
                {
                    _dbConnection.Close();
                }

            }
        }

        public void UpdateChallengeWinner(int challengeId, int winnerCode)
        {
            using (var command = _dbConnection.CreateCommand())
            {
                command.CommandText =
                    @"UPDATE DanisenChallenge SET ChallengeStatus = 'COMPLETE', WinnerChallengeCode = @winnerCode WHERE ChallengeId = @challengeId";

                var winnerCodeParm = command.CreateParameter();
                winnerCodeParm.DbType = DbType.Int32;
                winnerCodeParm.ParameterName = "winnerCode";
                winnerCodeParm.Value = winnerCode;
                command.Parameters.Add(winnerCodeParm);

                var challengeIdParm = command.CreateParameter();
                challengeIdParm.DbType = DbType.AnsiString;
                challengeIdParm.ParameterName = "challengeId";
                challengeIdParm.Value = challengeId;
                command.Parameters.Add(challengeIdParm);

                try
                {
                    EnsureConnection();
                    command.ExecuteNonQuery();
                    _cache.Remove("Challenges");
                }
                catch (Exception e)
                {
                    _logger.Error($"[{MethodBase.GetCurrentMethod().Name}] {e.Message}");

                }
                finally
                {
                    _dbConnection.Close();
                }

            }
        }

        public Rank IncrementPlayerPoints(int registrationCode)
        {
            return AdjustPlayerPoints(registrationCode, +1);
        }

        public Rank DecrementPlayerPoints(int registrationCode)
        {
            return AdjustPlayerPoints(registrationCode, -1);
        }

        private Rank AdjustPlayerPoints(int registrationCode, int movement)
        {
            // For safety
            _cache.Remove("RegisteredPlayers");
            var registration = GetRegisteredPlayers().First(rp => rp.RegistrationCode == registrationCode);
            registration.Points += movement;

            var newRank = registration.Rank;
            // Can't go lower than Beginner Rank
            if (registration.Rank.Level == 1 && registration.Points == -1)
            {
                return registration.Rank;
            }

            var ranks = GetRanks(registration.DanisenLeague.RankSetId);
            if (registration.Points <= registration.Rank.DemotionScore && registration.Rank.DemotionScore != 0)
            {
                // Demotion
                registration.Points = 0;
                newRank = ranks.First(r => r.Level == (registration.Rank.Level - 1));

            }
            else if (registration.Points >= registration.Rank.PromotionScore)
            {
                // Promotion
                registration.Points = 0;
                newRank = ranks.First(r => r.Level == (registration.Rank.Level + 1));
            }

            using (var command = _dbConnection.CreateCommand())
            {
                command.CommandText =
                    @"UPDATE DanisenRegistration SET Points = @points, RankId = @rankId WHERE RegistrationCode = @registrationCode";

                var winnerCodeParm = command.CreateParameter();
                winnerCodeParm.DbType = DbType.Int32;
                winnerCodeParm.ParameterName = "points";
                winnerCodeParm.Value = registration.Points;
                command.Parameters.Add(winnerCodeParm);

                var registrationCodeParm = command.CreateParameter();
                registrationCodeParm.DbType = DbType.Int32;
                registrationCodeParm.ParameterName = "registrationCode";
                registrationCodeParm.Value = registrationCode;
                command.Parameters.Add(registrationCodeParm);

                var rankIdParm = command.CreateParameter();
                rankIdParm.DbType = DbType.Int32;
                rankIdParm.ParameterName = "rankId";
                rankIdParm.Value = newRank.RankId;
                command.Parameters.Add(rankIdParm);

                try
                {
                    EnsureConnection();
                    command.ExecuteNonQuery();

                    return newRank;
                }
                catch (Exception e)
                {
                    _logger.Error($"[{MethodBase.GetCurrentMethod().Name}] {e.Message}");
                    throw;
                }
                finally
                {
                    _dbConnection.Close();
                }
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
                _cache?.Dispose();
                _cache = null;

                _dbConnection?.Dispose();
                _dbConnection = null;
            }
        }
    }
}
