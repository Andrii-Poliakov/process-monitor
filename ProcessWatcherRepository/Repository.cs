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
                    EndUtc    TEXT NOT NULL,
                    Status    INTEGER NOT NULL DEFAULT (0),
                    FOREIGN KEY (AppId) REFERENCES Apps(Id)
                );
                CREATE INDEX IF NOT EXISTS IX_AppRuns_AppId_Open ON AppRuns(AppId) WHERE EndUtc IS NULL;
                ");
        }

        /// <summary>
        /// Updates the status of all application runs from a specified status to a new status.
        /// </summary>
        /// <remarks>This method performs an update operation on the database to change the status of all
        /// application runs that match the specified <paramref name="fromStatus"/> to the specified <paramref
        /// name="toStatus"/>. Ensure that the provided status values are valid and correspond to the application's
        /// defined status codes.</remarks>
        /// <param name="fromStatus">The current status of the application runs to be updated.</param>
        /// <param name="toStatus">The new status to set for the application runs.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the number of rows affected by
        /// the update.</returns>
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

            // Update previous runs' status to closed
            await cn.ExecuteAsync(
                "UPDATE AppRuns SET Status = 1 WHERE AppId = @appId AND Status = 0;",
                new { appId });

            // Insert new run and get its Id
            var newRunId = await cn.ExecuteScalarAsync<int>(
                "INSERT INTO AppRuns (AppId, StartUtc, EndUtc) VALUES (@appId, @start, @start); SELECT last_insert_rowid();",
                new { appId, start = utcNow.ToString("o") });

            return newRunId;
            //// Query the newly inserted row
            //var row = await cn.QuerySingleAsync<AppRunInfoModel>(
            //    @"SELECT Id, AppId, StartUtc, EndUtc, Status 
            //      FROM AppRuns
            //      WHERE Id = @id",
            //    new { id = newRunId });

            //// Map to DTO
            //return new AppRunInfoDto
            //{
            //    Id = row.Id,
            //    AppId = row.AppId,
            //    StartUtc = DateTime.Parse(row.StartUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            //    EndUtc = row.EndUtc is null ? DateTime.MinValue : DateTime.Parse(row.EndUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
            //    Status = row.Status
            //};
        }

        public async Task<int> UpdateRunAsync(int appId, DateTime utcNow)
        {
            using var cn = Open();
            return await cn.ExecuteScalarAsync<int>(
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

    }
}
