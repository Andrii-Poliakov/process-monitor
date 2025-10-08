using Microsoft.Extensions.Logging;
using ProcessMonitorRepository;
using ProcessWatcherShared;
using System.Collections.Generic;
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
        private Dictionary<int, (AppRunInfoDto, AppInfoDto)> _appRunInfoDictionaryById = new();

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
            _logger.LogInformation("ScanOnce Start");

            var processes = SafeGetProcessesWithUi().ToList();
            int addCount = 0;
            foreach (var p in processes)
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

                (AppRunInfoDto appRunInfo, AppInfoDto appInfo) appRunInfoTupl;

                // We already have it as running, change end time to now
                if ( _appRunInfoDictionaryById.TryGetValue(appId, out appRunInfoTupl))
                {
                    await _repository.UpdateRunAsync(appId, utcNow);
                    appRunInfoTupl.appRunInfo.EndUtc = utcNow;

                }
                else
                {
                    // If you did not previously consider the process to be running, open a session.
                    var appRunId = await _repository.OpenRunAsync(appId, utcNow);
                    //_runningAppsId.Add(appId);

                    appRunInfoTupl = (
                        new AppRunInfoDto
                        {
                            Id = appRunId,
                            AppId = appId,
                            StartUtc = utcNow,
                            EndUtc = utcNow,
                            Status = 0
                        }, 
                        appInfo);

                    addCount++;
                    _appRunInfoDictionaryById.Add(appId, appRunInfoTupl);
                }
            }

            // We are closing sessions for those who are missing.

            var toClose = _appRunInfoDictionaryById.Keys.Except(seenAppIds).ToList();
            foreach (var appId in toClose)
            {
                await _repository.CloseOpenRunAsync(appId, utcNow);
                _appRunInfoDictionaryById.Remove(appId);
            }

            _logger.LogInformation($"ScanOnce End {addCount}+ / {toClose?.Count}-");
        }

        public async Task BlockOnce(CancellationToken ct)
        {
            _logger.LogInformation("BlockOnce Start");
            // Use case-insensitive comparers for Windows file system and process names.
            var ci = StringComparer.OrdinalIgnoreCase;

            // HashSet for blocked names (case-insensitive)
            var blockedNames = new HashSet<string>(ci);

            var blockedApps = await _repository.GetBlockedAppsAsync().ConfigureAwait(false);

            // Go through all blocked apps 
            foreach (var blockedApp in blockedApps) 
            {
                // Go through all running apps
                foreach (var runItem in _appRunInfoDictionaryById)
                {
                    // Check for cancellation
                    if (ct.IsCancellationRequested) break;

                    // Block by Path
                    if (blockedApp.BlockType == 1)
                    {
                        if (string.Equals(blockedApp.BlockValue, runItem.Value.Item2.FullPath, StringComparison.OrdinalIgnoreCase))
                        {
                            blockedNames.Add(runItem.Value.Item2.Name);
                        }
                    }

                    // Block by App Name
                    else if (blockedApp.BlockType == 2)
                    {
                        if (string.Equals(blockedApp.BlockValue, runItem.Value.Item2.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            blockedNames.Add(runItem.Value.Item2.Name);
                        }
                    }
                    // Block by AppId
                    else if (blockedApp.BlockType == 4)
                    {
                        if (blockedApp.BlockValue == runItem.Value.Item1.AppId.ToString())
                        {
                            blockedNames.Add(runItem.Value.Item2.Name);
                        }
                    }

                }
            }

            // Now we have a list of blocked process names, we can kill them
            KillBlockedProcesses(blockedNames, ct);
            _logger.LogInformation("BlockOnce End");
        }




        private void KillBlockedProcesses(HashSet<string> blockedNames, CancellationToken ct)
        {
            if (blockedNames is null || blockedNames.Count == 0) return;

            Process[] snapshot;
            try { snapshot = Process.GetProcesses(); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enumerate processes.");
                return;
            }

            foreach (var proc in snapshot)
            {
                
                if (ct.IsCancellationRequested) break;
                using (proc)
                {
                    string name;
                    try { name = proc.ProcessName; }
                    catch { continue; }

                    // HashSet already has case-insensitive comparer
                    if (!blockedNames.Contains(name)) continue;

                    try
                    {
                        _logger.LogInformation($"KillBlockedProcesses {proc.ProcessName}");
                        proc.Kill(); // .NET 4.8: без entireProcessTree
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to kill process '{Name}' (PID={Pid}).", name, SafePid(proc));
                    }
                }
            }

            static int SafePid(Process p) { try { return p.Id; } catch { return -1; } }
        }



        private List<ProcessInfoDto> SafeGetProcessesWithUi()
        {
            _logger.LogInformation("SafeGetProcessesWithUi Start");
            Process[] all = Array.Empty<Process>();
            List<ProcessInfoDto> result = new List<ProcessInfoDto>();
            try
            {
                all = Process.GetProcesses();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get processes.");
            }


            if (all != null)
            {


                foreach (var process in all)
                {
                    using (process)
                    {
                        //IntPtr h = IntPtr.Zero;
                        //string title = null;

                        //try
                        //{
                        //    h = process.MainWindowHandle;
                        //    title = process.MainWindowTitle;
                        //}
                        //catch
                        //{
                        //    _logger.LogInformation($"SafeGetProcessesWithUi 5 Skip if no access");
                        //    continue; /* Skip if no access */
                        //}

                        //if (h == IntPtr.Zero || string.IsNullOrWhiteSpace(title))
                        //{
                        //    _logger.LogInformation($"SafeGetProcessesWithUi 6 IntPtr.Zero");
                        //    continue;
                        //}

                        var exePath = TryGetExePath(process);
                        if (exePath == null)
                        {
                            continue;
                        }

                        result.Add(
                                    new ProcessInfoDto
                                    {
                                        ProcessId = process.Id,
                                        Name = process.ProcessName,
                                        FullPath = NormalizePath(exePath),
                                        StartDateTime = process.StartTime,
                                        EndDateTime = null
                                    });
                    }
                }
            }
            _logger.LogInformation($"SafeGetProcessesWithUi End {result.Count}");
            return result;
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
