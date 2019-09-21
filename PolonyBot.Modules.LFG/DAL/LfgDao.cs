using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.IO;
using System.Threading.Tasks;

namespace PolonyBot.Modules.LFG.DAL
{
    public interface ILfgDao
    {
        void Init();
        Task InsertCommand(ulong userId, string userName, string lfgCommand, string game);
        Task<DataTable> GetGeneralStats();
    }

    internal class LfgDao : ILfgDao
    {
        private const string DataBaseName = "PolonyBot.db";

        public LfgDao()
        {
        }

        public async void Init()
        {
            if (!File.Exists(DataBaseName))
            {
                SQLiteConnection.CreateFile(DataBaseName);
            }

            const string commandText = @"CREATE TABLE IF NOT EXISTS LFG 
(
	Id	        INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
	UserId	    NUMERIC NOT NULL,
	UserName	TEXT NOT NULL,
	Command	    TEXT NOT NULL,
	Game	    TEXT NOT NULL,
	Timestamp	TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);";
            using (var dbConnection = CreateConnection())
            using (var command = dbConnection.CreateCommand())
            {
                command.CommandText = commandText;
                await dbConnection.OpenAsync().ConfigureAwait(false);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task InsertCommand(ulong userId, string userName, string lfgCommand, string game)
        {
            const string commandText = "INSERT INTO LFG (UserId, UserName, Command, Game) VALUES (?, ?, ?, ?)";

            using (var dbConnection = CreateConnection())
            using (var command = dbConnection.CreateCommand())
            {
                command.CommandText = commandText;

                var userIdParm = command.CreateParameter();
                userIdParm.DbType = DbType.Int64;
                userIdParm.Value = userId;

                var userNameParm = command.CreateParameter();
                userNameParm.DbType = DbType.AnsiString;
                userNameParm.Value = userName;

                var commandParm = command.CreateParameter();
                commandParm.DbType = DbType.AnsiString;
                commandParm.Value = lfgCommand;

                var gameParm = command.CreateParameter();
                gameParm.DbType = DbType.AnsiString;
                gameParm.Value = game.ToUpper();

                command.Parameters.AddRange(new [] { userIdParm, userNameParm, commandParm, gameParm });
                await dbConnection.OpenAsync().ConfigureAwait(false);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<DataTable> GetGeneralStats()
        {
            const string commandText = @"SELECT lfg1.Game
, COUNT(*) AS TimesRequested
, COUNT(DISTINCT Userid) AS UniqueUsersRequested
, (SELECT Max(Timestamp) FROM LFG lfg2 WHERE lfg2.Game = lfg1.Game) AS LastRequested
, (	SELECT  lfg3.UserName || ' (' || COUNT(*) || ')' 
	FROM LFG lfg3 
	WHERE lfg3.Game = lfg1.Game AND lfg3.Command = lfg1.Command 
	GROUP BY lfg3.UserName 
	ORDER BY COUNT(*) DESC LIMIT 1
) AS MostRequestedBy
FROM LFG lfg1
WHERE lfg1.Command = 'ADD'
GROUP BY lfg1.Game
ORDER BY TimesRequested DESC";

            using (var dbConnection = CreateConnection())
            using (var command = dbConnection.CreateCommand())
            {
                command.CommandText = commandText;

                await dbConnection.OpenAsync().ConfigureAwait(false);
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    var dataTable = new DataTable();
                    dataTable.Load(reader);

                    return dataTable;
                }
            }
        }

        private static DbConnection CreateConnection()
        {
            var connectionString = $"Data Source={DataBaseName};Version=3;";
            
            // Yes, I know...
            return new SQLiteConnection(connectionString);
        }
    }
}
