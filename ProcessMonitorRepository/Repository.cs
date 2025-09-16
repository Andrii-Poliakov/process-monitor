using Dapper;
using Microsoft.Data.Sqlite;
using System.Data;
using System.IO;
using SQLitePCL;


namespace ProcessMonitorRepository
{
    public class Repository
    {
        private readonly string _dbPath;
        private readonly string _connStr;

        public Repository()
        {
            // Init SQLite native provider
            Batteries_V2.Init();

            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "ProcessTracker");
            Directory.CreateDirectory(baseDir);
            _dbPath = Path.Combine(baseDir, "tracker.db");
            _connStr = $"Data Source={_dbPath};";
            EnsureSchema().GetAwaiter().GetResult();
        }

        private IDbConnection Open() => new SqliteConnection(_connStr);

        private async Task EnsureSchema()
        {
            using var cn = Open();
            await cn.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS Apps (
                    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name      TEXT NOT NULL,
                    FullPath  TEXT NOT NULL UNIQUE,
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
                );
                CREATE TABLE IF NOT EXISTS AppRuns (
                    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
                    AppId     INTEGER NOT NULL,
                    StartUtc  TEXT NOT NULL,
                    EndUtc    TEXT NULL,
                    FOREIGN KEY (AppId) REFERENCES Apps(Id)
                );
                CREATE INDEX IF NOT EXISTS IX_AppRuns_AppId_Open ON AppRuns(AppId) WHERE EndUtc IS NULL;
                ");
        }

        public async Task<int> UpsertAppAsync(string name, string fullPath)
        {
            using var cn = Open();
            
            var appId = await cn.ExecuteScalarAsync<int?>(
                "SELECT Id FROM Apps WHERE FullPath = @fullPath COLLATE NOCASE",
                new { fullPath }) ?? 0;

            if (appId != 0) return appId;

            
            return await cn.ExecuteScalarAsync<int>(
                "INSERT INTO Apps (Name, FullPath) VALUES (@name, @fullPath); SELECT last_insert_rowid();",
                new { name, fullPath });
        }

        public async Task<bool> HasOpenRunAsync(int appId)
        {
            using var cn = Open();
            var id = await cn.ExecuteScalarAsync<int?>(
                "SELECT Id FROM AppRuns WHERE AppId = @appId AND EndUtc IS NULL LIMIT 1",
                new { appId });
            return id.HasValue;
        }

        public async Task<int> OpenRunAsync(int appId, DateTime utcNow)
        {
            using var cn = Open();
            return await cn.ExecuteScalarAsync<int>(
                "INSERT INTO AppRuns (AppId, StartUtc) VALUES (@appId, @start); SELECT last_insert_rowid();",
                new { appId, start = utcNow.ToString("o") });
        }

        public async Task<int> CloseOpenRunAsync(int appId, DateTime utcNow)
        {
            using var cn = Open();
            return await cn.ExecuteAsync(
                "UPDATE AppRuns SET EndUtc = @end WHERE AppId = @appId AND EndUtc IS NULL",
                new { appId, end = utcNow.ToString("o") });
        }
    }
}
