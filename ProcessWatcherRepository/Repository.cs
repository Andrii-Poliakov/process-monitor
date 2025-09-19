using Dapper;
using Microsoft.Data.Sqlite;
using ProcessWatcherShared;
using SQLitePCL;
using System.Data;
using System.Globalization;
using System.IO;


namespace ProcessMonitorRepository
{
    public class Repository
    {
        public sealed record AppRow(int Id, string Name, string FullPath, string CreatedAt);

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
                    Status    INTEGER NOT NULL DEFAULT (0),
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

        public async Task<List<AppInfoDto>> GetAppsAsync()
        {
            try
            {
                using var cn = Open();

                var rows = await cn.QueryAsync<AppInfoModel>(
                    @"SELECT Id, Name, FullPath, CreatedAt
                      FROM Apps
                      ORDER BY Name COLLATE NOCASE, Id");

                return rows
                    .Select(static row => new AppInfoDto()
                        {

                            Id = row.Id,
                            Name = row.Name,
                            FullPath = row.FullPath,
                            CreatedAt = DateTime.Parse(
                                row.CreatedAt,
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
                        }
                    )
                    .ToList();
            }
            catch (Exception ex)
            {

                throw;
            }

        }

        public async Task<IReadOnlyList<AppRunInfoDto>> GetAppRunsAsync()
        {
            using var cn = Open();

            var rows = await cn.QueryAsync<AppRunInfoModel>(
                @"SELECT Id, AppId, StartUtc, EndUtc
                  FROM AppRuns
                  ORDER BY Id desc
                  LIMIT 100");

            return rows
                .Select(static row => new AppRunInfoDto()
                {

                    Id = row.Id,
                    AppId = row.AppId,
                    StartUtc = DateTime.Parse(
                        row.StartUtc,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                    EndUtc = row.EndUtc is null ? DateTime.MinValue : DateTime.Parse(
                        row.EndUtc,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
                }
                )
                .ToList();
        }

    }
}
