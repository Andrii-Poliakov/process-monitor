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
                    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name            TEXT NOT NULL,
                    FullPath        TEXT NOT NULL UNIQUE,
                    CreatedAt       TEXT NOT NULL DEFAULT (datetime('now'))
                );
                CREATE TABLE IF NOT EXISTS AppRuns (
                    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    AppId           INTEGER NOT NULL,
                    StartUtc        TEXT NOT NULL,
                    EndUtc          TEXT NOT NULL,
                    Status          INTEGER NOT NULL DEFAULT (0),
                    FOREIGN KEY (AppId) REFERENCES Apps(Id)
                );
                CREATE INDEX IF NOT EXISTS IX_AppRuns_AppId_Open ON AppRuns(AppId);

                CREATE TABLE IF NOT EXISTS BlockedApps (
                    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    BlockType       INTEGER NOT NULL DEFAULT (0),
                    BlockValue      TEXT NOT NULL,
                    CreatedAt       TEXT NOT NULL DEFAULT (datetime('now')),                    
                    UpdatedAt       TEXT NOT NULL DEFAULT (datetime('now'))
                );

                CREATE TABLE IF NOT EXISTS AppRunStatuses (
                    Id              INTEGER PRIMARY KEY,
                    Name            TEXT NOT NULL
                );

                INSERT OR IGNORE INTO AppRunStatuses (Id, Name) VALUES (0, 'Unknown');                
                INSERT OR IGNORE INTO AppRunStatuses (Id, Name) VALUES (1, 'Opened');
                INSERT OR IGNORE INTO AppRunStatuses (Id, Name) VALUES (2, 'Closed');

                CREATE TABLE IF NOT EXISTS BlockTypes (
                    Id              INTEGER PRIMARY KEY,
                    Name            TEXT NOT NULL
                );

                INSERT OR IGNORE INTO BlockTypes (Id, Name) VALUES (0, 'Unknown');
                INSERT OR IGNORE INTO BlockTypes (Id, Name) VALUES (1, 'FullPath');
                INSERT OR IGNORE INTO BlockTypes (Id, Name) VALUES (2, 'ProcessName');
                INSERT OR IGNORE INTO BlockTypes (Id, Name) VALUES (3, 'WindowTitle');
                INSERT OR IGNORE INTO BlockTypes (Id, Name) VALUES (4, 'AppId');

                ");
        }

        public async Task<int> SetAllAppRunsStatusAsync(int fromStatus, int toStatus)
        {
            using var cn = Open();

            return await cn.ExecuteAsync(
                "UPDATE AppRuns SET Status = @toStatus WHERE Status = @fromStatus",
                new { fromStatus, toStatus });
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

        public async Task<int> OpenRunAsync(int appId, DateTime utcNow)
        {
            using var cn = Open();

            // Update previous runs' status to closed
            await cn.ExecuteAsync(
                "UPDATE AppRuns SET Status = 1 WHERE AppId = @appId AND Status = 0;",
                new { appId });

            // Insert new run and get its Id
            var newRunId = await cn.ExecuteScalarAsync<int>(
                "INSERT INTO AppRuns (AppId, StartUtc, EndUtc) VALUES (@appId, @start, @start); SELECT last_insert_rowid();",
                new { appId, start = utcNow.ToString("o") });

            return newRunId;
        }

        public async Task<int> UpdateRunAsync(int appId, DateTime utcNow)
        {
            using var cn = Open();
            return await cn.ExecuteAsync(
                "UPDATE AppRuns SET EndUtc = @end WHERE AppId = @appId AND Status = 0; ",
                new { appId, end = utcNow.ToString("o") });
        }

        public async Task<int> CloseOpenRunAsync(int appId, DateTime utcNow)
        {
            using var cn = Open();
            return await cn.ExecuteAsync(
                "UPDATE AppRuns SET Status = 1, EndUtc = @end WHERE AppId = @appId AND Status = 0",
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

        public async Task<List<AppInfoDto>> GetAppsExAsync()
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
                @"SELECT Id, AppId, StartUtc, EndUtc, Status 
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
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                    Status = row.Status

                }
                )
                .ToList();
        }


        public async Task<IReadOnlyList<BlockedAppDto>> GetBlockedAppsAsync()
        {
            using var cn = Open();

            var rows = await cn.QueryAsync<BlockedAppModel>(
                @"SELECT ba.Id,
                         ba.BlockType,
                         COALESCE(bt.Name, 'Unknown') AS BlockTypeName,
                         ba.BlockValue,
                         ba.CreatedAt,
                         ba.UpdatedAt
                  FROM BlockedApps AS ba
                  LEFT JOIN BlockTypes AS bt ON ba.BlockType = bt.Id
                  ORDER BY ba.BlockValue COLLATE NOCASE, ba.Id;");

            return rows
                .Select(static row => MapBlockedApp(row))
                .ToList();
        }

        public async Task<IReadOnlyList<BlockTypeDto>> GetBlockTypesAsync()
        {
            using var cn = Open();

            var rows = await cn.QueryAsync<BlockTypeModel>(
                @"SELECT Id,
                         Name
                  FROM BlockTypes
                  ORDER BY Id;");

            return rows
                .Select(static row => new BlockTypeDto
                {
                    Id = row.Id,
                    Name = row.Name
                })
                .ToList();
        }

        public async Task<BlockedAppDto?> AddBlockedAppAsync(int blockType, string blockValue)
        {
            using var cn = Open();

            var newId = await cn.ExecuteScalarAsync<long>(
                @"INSERT INTO BlockedApps (BlockType, BlockValue)
                  VALUES (@blockType, @blockValue);
                  SELECT last_insert_rowid();",
                new { blockType, blockValue });

            return await GetBlockedAppByIdAsync(cn, (int)newId);
        }

        public async Task<BlockedAppDto?> UpdateBlockedAppAsync(int id, int blockType, string blockValue)
        {
            using var cn = Open();

            var affected = await cn.ExecuteAsync(
                @"UPDATE BlockedApps
                  SET BlockType = @blockType,
                      BlockValue = @blockValue,
                      UpdatedAt = datetime('now')
                  WHERE Id = @id;",
                new { id, blockType, blockValue });

            if (affected == 0)
            {
                return null;
            }

            return await GetBlockedAppByIdAsync(cn, id);
        }

        public async Task<bool> DeleteBlockedAppAsync(int id)
        {
            using var cn = Open();

            var affected = await cn.ExecuteAsync(
                "DELETE FROM BlockedApps WHERE Id = @id;",
                new { id });

            return affected > 0;
        }

        private static BlockedAppDto MapBlockedApp(BlockedAppModel row)
        {
            return new BlockedAppDto
            {
                Id = row.Id,
                BlockType = row.BlockType,
                BlockTypeName = row.BlockTypeName,
                BlockValue = row.BlockValue,
                CreatedAt = DateTime.Parse(
                    row.CreatedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                UpdatedAt = DateTime.Parse(
                    row.UpdatedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
            };
        }

        private static async Task<BlockedAppDto?> GetBlockedAppByIdAsync(IDbConnection cn, int id)
        {
            var model = await cn.QuerySingleOrDefaultAsync<BlockedAppModel>(
                @"SELECT ba.Id,
                         ba.BlockType,
                         COALESCE(bt.Name, 'Unknown') AS BlockTypeName,
                         ba.BlockValue,
                         ba.CreatedAt,
                         ba.UpdatedAt
                  FROM BlockedApps AS ba
                  LEFT JOIN BlockTypes AS bt ON ba.BlockType = bt.Id
                  WHERE ba.Id = @id;",
                new { id });

            return model is null ? null : MapBlockedApp(model);
        }


    }
}
