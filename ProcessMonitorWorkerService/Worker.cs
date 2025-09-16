using ProcessMonitorRepository;
using System.Diagnostics;

namespace ProcessMonitorWorkerService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _log;
        private readonly Repository _repo;

        // The period between scans
        private readonly TimeSpan _period = TimeSpan.FromSeconds(5);

        // The set of currently running AppIds
        private readonly HashSet<int> _running = new HashSet<int>();

        public Worker(Repository repo, ILogger<Worker> logger)
        {
            _log = logger;
            _repo = repo;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("ProcessWatcher started");

            while (!stoppingToken.IsCancellationRequested)
            {
                if (_log.IsEnabled(LogLevel.Information))
                    _log.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                try
                {
                    await ScanOnce(stoppingToken);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "ScanOnce error");
                }

                try
                {
                    await Task.Delay(_period, stoppingToken);
                }
                catch (TaskCanceledException) { /* graceful exit */ }
            }

        }


        private async Task ScanOnce(CancellationToken ct)
        {
            var seenAppIds = new HashSet<int>();
            var utcNow = DateTime.UtcNow;

            foreach (var p in SafeGetProcessesWithUi())
            {
                if (ct.IsCancellationRequested) break;

                var exe = TryGetExePath(p);
                if (exe == null) continue;

                var full = NormalizePath(exe);
                var name = p.ProcessName; 

                // Registration AppId
                var appId = await _repo.UpsertAppAsync(name, full);
                seenAppIds.Add(appId);

                // If you did not previously consider the process to be running, open a session.
                if (!_running.Contains(appId))
                {
                    await _repo.OpenRunAsync(appId, utcNow);
                    _running.Add(appId);
                }
            }

            // We are closing sessions for those who are missing.
            var toClose = _running.Except(seenAppIds).ToList();
            foreach (var appId in toClose)
            {
                await _repo.CloseOpenRunAsync(appId, utcNow);
                _running.Remove(appId);
            }
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
