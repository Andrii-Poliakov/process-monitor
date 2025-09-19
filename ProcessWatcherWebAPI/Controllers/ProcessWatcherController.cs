using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProcessMonitorRepository;
using ProcessWatcherShared;

namespace ProcessWatcherWebAPI.Controllers
{
    [ApiController]
    [Route("api")]
    public class ProcessWatcherController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<ProcessWatcherController> _logger;
        private readonly Repository _repository;

        public ProcessWatcherController(ILogger<ProcessWatcherController> logger, Repository repository)
        {
            _logger = logger;
            _repository = repository;
        }

        [HttpGet("get-weather")]
        public IEnumerable<WeatherForecast> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }

        [HttpGet("get-apps")]
        public IEnumerable<AppInfoDto> GetApps()
        {
            return _repository.GetAppsAsync().Result.ToArray();
        }

        [HttpGet("get-app-runs")]
        public IEnumerable<AppRunInfoDto> GetAppRunsAsync()
        {
            return _repository.GetAppRunsAsync().Result.ToArray();
        }

    }
}
