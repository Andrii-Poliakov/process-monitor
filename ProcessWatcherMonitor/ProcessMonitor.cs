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

            foreach (var p in GetProcessList())
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




        public List<ProcessInfoDto> GetProcessList()
        {
            List<ProcessInfoDto> processInfoDtos = new List<ProcessInfoDto>();

            var utcNow = DateTime.UtcNow;

            foreach (var p in SafeGetProcessesWithUi())
            {
                var exePath = TryGetExePath(p);
                if (exePath == null) continue;

                var fullPath = NormalizePath(exePath);
                var processName = p.ProcessName;

                var dto = new ProcessInfoDto
                {
                    ProcessId = p.Id,
                    Name = processName,
                    FullPath = fullPath,
                    StartDateTime = p.StartTime,
                    EndDateTime = null
                };
                processInfoDtos.Add(dto);

            }

            return processInfoDtos;

        }



        /// <summary>
        /// Retrieves a collection of processes that have a user interface (UI) by filtering processes with a valid main
        /// window handle and a non-empty window title.
        /// </summary>
        /// <remarks>This method safely enumerates all processes on the system and filters out those that
        /// do not have a user interface. Processes without a valid main window handle or with an empty or 
        /// whitespace-only window title are excluded. If access to a process is denied, it is skipped.</remarks>
        /// <returns>An enumerable collection of <see cref="Process"/> objects representing processes with a user interface.</returns>
        private static IEnumerable<Process> SafeGetProcessesWithUi()
        {
            Process[] all;
            try { all = Process.GetProcesses(); }
            catch { yield break; }

            foreach (var p in all)
            {
                IntPtr h = IntPtr.Zero;
                string title = null;

                try
                {
                    h = p.MainWindowHandle;
                    title = p.MainWindowTitle;
                }
                catch { /* Skip if no access */ }

                if (h != IntPtr.Zero && !string.IsNullOrWhiteSpace(title))
                    yield return p;
                else
                    p.Dispose();
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
