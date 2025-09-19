using ProcessWatcherMonitor;

namespace ProcessWatcherWorkerService
{
    public class Worker : BackgroundService
    {
        // The period between scans
        private readonly TimeSpan _period = TimeSpan.FromSeconds(5);

        private readonly ILogger<Worker> _logger;
        private readonly ProcessMonitor _processMonitor;

        public Worker(ILogger<Worker> logger, ProcessMonitor processMonitor)
        {
            _logger = logger;
            _processMonitor = processMonitor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                try
                {
                    await _processMonitor.ScanOnce(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ScanOnce error");
                }

                try
                {
                    await Task.Delay(_period, stoppingToken);
                }
                catch (TaskCanceledException) { /* graceful exit */ }
            }
        }
    }
}
