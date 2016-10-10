using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Runtime.Caching;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Polony.Dal.Model;

namespace Polony.Dal
{
    public class DanisenDao
    {
        private readonly ObjectCache _cache = MemoryCache.Default;
        private readonly DbConnection _dbConnection;

        public DanisenDao(string connectionString)
        {
            var factory = DbProviderFactories.GetFactory("System.Data.SqlClient");
            _dbConnection = factory.CreateConnection();
            _dbConnection.ConnectionString = connectionString;
        }

        public List<Game> GetGames()
        {
            if (!_cache.Contains("GamesList"))
            { 
                using (var command = _dbConnection.CreateCommand())
                {
                    command.CommandText = "SELECT GameId, Name, ShortName FROM Game";
                    command.CommandType = CommandType.Text;

                    _dbConnection.Open();
                    using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
                    {
                        var gameList = new List<Game>();

                        while (reader.Read())
                        {
                            var gameId = reader.GetFieldValue<int>(0);
                            var name = reader.GetFieldValue<string>(1);
                            var shortName = reader.GetFieldValue<string>(2);

                            gameList.Add(new Game(gameId, name, shortName));

                            var policy = new CacheItemPolicy {AbsoluteExpiration = DateTimeOffset.Now.AddHours(12)};

                            _cache.Add("GameList", gameList, policy);

                        }
                    }
                }
            }

            return _cache["GameList"] as List<Game>;
        }


        public List<DanisenRegistration> GetRegisteredPlayers(string gameAlias)
        {
            if (!_cache.Contains("RegisteredPlayers"))
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
FROM DanisenRegistration DR
JOIN [dbo].[DanisenLeague] DL
    ON [DL].[DanisenLeagueId] = [DR].[DanisenLeagueId]
JOIN [dbo].[Game] G
    ON [G].[GameId] = [DL].[GameId]
JOIN [dbo].[Player] P
    ON [P].[PlayerId] = [DR].[PlayerId]
WHERE [G].[ShortName] = ?";

                    command.CommandType = CommandType.Text;
                    var gameNameParm = command.CreateParameter();
                    gameNameParm.DbType = DbType.AnsiString;
                    gameNameParm.ParameterName = "GameShortName";
                    gameNameParm.Value = gameAlias;

                    command.Parameters.Add(gameNameParm);

                    _dbConnection.Open();
                    using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
                    {
                        var leagueList = new List<DanisenRegistration>();

                        while (reader.Read())
                        {
                            var gameId = reader.GetFieldValue<int>(0);
                            var name = reader.GetFieldValue<string>(1);
                            var shortName = reader.GetFieldValue<string>(2);
                            var leagueId = reader.GetFieldValue<int>(3);
                            var league = new DanisenLeague
                            {
                                DanisenLeagueId = leagueId,
                                Game = new Game(gameId, name, shortName),
                                MultipleCharactersAllowed = reader.GetFieldValue<bool>(4),
                            };

                            leagueList.Add(new DanisenRegistration
                            {
                                
                            });
                        }

                        var policy = new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddHours(12) };
                        _cache.Add("RegisteredPlayers", leagueList, policy);
                    }
                }
            }

            return _cache["RegisteredPlayers"] as List<DanisenRegistration>;
        }

        public List<DanisenLeague> GetAvalailableDanisenLeagues()
        {
            if (!_cache.Contains("LeagueList"))
            {
                using (var command = _dbConnection.CreateCommand())
                {
                    command.CommandText = @"SELECT G.GameId
, G.Name
, G.ShortName
, DL.DanisenLeagueId
, DL.MultipleCharactersAllowed
FROM DanisenLeague DL
JOIN Game G
    ON DL.GameId = G.GameId
WHERE DL.Enabled = 1";
                    
                    command.CommandType = CommandType.Text;

                    _dbConnection.Open();
                    using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
                    {
                        var leagueList = new List<DanisenLeague>();

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
                            });
                        }

                        var policy = new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddHours(12) };
                        _cache.Add("LeagueList", leagueList, policy);
                    }
                }
            }

            return _cache["LeagueList"] as List<DanisenLeague>;
        }


        public List<Rank> GetRanks()
        {
            if (!_cache.Contains("RankList"))
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
ORDER BY [Level]
";

                    command.CommandType = CommandType.Text;

                    _dbConnection.Open();
                    using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
                    {
                        var list = new List<Rank> ();

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

                            var policy = new CacheItemPolicy { AbsoluteExpiration = DateTimeOffset.Now.AddHours(12) };

                            _cache.Add("RankList", list, policy);

                        }
                    }
                }
            }

            return _cache["RankList"] as List<Rank>;
        }
    }
}
