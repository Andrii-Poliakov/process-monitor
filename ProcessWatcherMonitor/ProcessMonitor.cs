using Microsoft.Extensions.Logging;
using ProcessMonitorRepository;
using ProcessWatcherShared;
using System.Diagnostics;
using System.Linq;

namespace ProcessWatcherMonitor
{

    public class ProcessMonitor
    {

        private readonly ILogger<ProcessMonitor> _logger;
        private readonly Repository _repository;

        private Dictionary<string, AppInfoDto> _appInfoBlockDictionaryByName = new();

        private Dictionary<string, AppInfoDto> _appInfoDictionaryByPath = new();
        private Dictionary<int, AppRunInfoDto> _appRunInfoDictionaryById = new();

        // The set of currently running AppIds
        //private readonly HashSet<int> _runningAppsId = new HashSet<int>();

        public ProcessMonitor(ILogger<ProcessMonitor> logger, Repository repository)
        {
            _logger = logger;
            _repository = repository;

            Initialize();
        }


        private void Initialize()
        {
            // Load all known apps from DB
            _repository.GetAppsAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Failed to load apps from database.");
                    return;
                }
                var apps = t.Result;
                _appInfoDictionaryByPath = apps.ToDictionary(a => a.FullPath, a => a);
            });


            // Set all running sessions to status=1 (Closed)
            _repository.SetAllAppRunsStatusAsync(0, 1).GetAwaiter().GetResult();
        }


        public async Task ScanOnce(CancellationToken ct)
        {
            var seenAppIds = new HashSet<int>();
            var utcNow = DateTime.UtcNow;

            foreach (var p in SafeGetProcessesWithUi())
            {
                if (ct.IsCancellationRequested) break;

                int appId = 0;
                if (_appInfoDictionaryByPath.TryGetValue(p.FullPath, out AppInfoDto appInfo)
                    && appInfo != null)
                {
                    appId = appInfo.Id;
                }
                else
                {
                    // Registration App in DB
                    appId = await _repository.UpsertAppAsync(p.Name, p.FullPath);
                    appInfo = new AppInfoDto
                    {
                        Id = appId,
                        Name = p.Name,
                        FullPath = p.FullPath,
                        CreatedAt = DateTime.UtcNow
                    };

                    // Cache it
                    _appInfoDictionaryByPath[p.FullPath] = appInfo;
                }

                seenAppIds.Add(appId);

                AppRunInfoDto appRunInfo;

                // We already have it as running, change end time to now
                if ( _appRunInfoDictionaryById.TryGetValue(appId, out appRunInfo))
                {
                    await _repository.UpdateRunAsync(appId, utcNow);
                    appRunInfo.EndUtc = utcNow;

                }
                else
                {
                    // If you did not previously consider the process to be running, open a session.
                    var appRunId = await _repository.OpenRunAsync(appId, utcNow);
                    //_runningAppsId.Add(appId);

                    appRunInfo = new AppRunInfoDto
                    {
                        Id = appRunId,
                        AppId = appId,
                        StartUtc = utcNow,
                        EndUtc = utcNow,
                        Status = 0
                    };
                    _appRunInfoDictionaryById.Add(appId, appRunInfo);
                }
            }

            // We are closing sessions for those who are missing.

            var toClose = _appRunInfoDictionaryById.Keys.Except(seenAppIds).ToList();
            foreach (var appId in toClose)
            {
                await _repository.CloseOpenRunAsync(appId, utcNow);
                _appRunInfoDictionaryById.Remove(appId);
            }
        }

        public async Task BlockOnce(CancellationToken ct)
        {

            var blockedApps = await _repository.GetBlockedAppsAsync();

            foreach (var blockItem in blockedApps)
            {

                if (blockItem.BlockType == 4)
                {
                    foreach (var runItem in _appRunInfoDictionaryById)
                    {
                        if (ct.IsCancellationRequested) break;

                        if (blockItem.BlockValue == runItem.Value.AppId.ToString())
                        {

                            AppInfoDto appInfoToBlock = _appInfoDictionaryByPath.Values
                                .FirstOrDefault(a => a.Id == runItem.Value.AppId);

                            if (appInfoToBlock != null)
                            {
                                try
                                {
                                    var process = Process.GetProcessesByName(appInfoToBlock.Name);
                                    foreach (var p in process)
                                    {
                                        try
                                        {
                                            p.Kill();
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogError(ex, $"Failed to kill process with AppId {runItem.Value.AppId}");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, $"Failed to kill process with AppId {runItem.Value.AppId}");
                                }
                            }
                        }

                    }
                }
            }




        }

        private static IEnumerable<ProcessInfoDto> SafeGetProcessesWithUi()
        {
            Process[] all;
            try { all = Process.GetProcesses(); }
            catch { yield break; }

            foreach (var process in all)
            {
                using (process)
                {
                    IntPtr h = IntPtr.Zero;
                    string title = null;

                    try
                    {
                        h = process.MainWindowHandle;
                        title = process.MainWindowTitle;
                    }
                    catch
                    {
                        continue; /* Skip if no access */
                    }

                    if (h == IntPtr.Zero || string.IsNullOrWhiteSpace(title))
                    {
                        continue;
                    }

                    var exePath = TryGetExePath(process);
                    if (exePath == null)
                    {
                        continue;
                    }

                    yield return new ProcessInfoDto
                    {
                        ProcessId = process.Id,
                        Name = process.ProcessName,
                        FullPath = NormalizePath(exePath),
                        StartDateTime = process.StartTime,
                        EndDateTime = null
                    };
                }
            }
        }







        private static string TryGetExePath(Process p)
        {
            try
            {
                return p.MainModule?.FileName;
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizePath(string path)
            => Path.GetFullPath(path.Trim().Trim('"'));

    }
}
