using Microsoft.AspNetCore.Http;
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
        private readonly ILogger<ProcessWatcherController> _logger;
        private readonly Repository _repository;

        public ProcessWatcherController(ILogger<ProcessWatcherController> logger, Repository repository)
        {
            _logger = logger;
            _repository = repository;
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

        [HttpGet("blocked-apps")]
        public async Task<IEnumerable<BlockedAppDto>> GetBlockedAppsAsync()
        {
            var blockedApps = await _repository.GetBlockedAppsAsync();
            return blockedApps.ToArray();
        }

        [HttpGet("block-types")]
        public async Task<IEnumerable<BlockTypeDto>> GetBlockTypesAsync()
        {
            var blockTypes = await _repository.GetBlockTypesAsync();
            return blockTypes.ToArray();
        }

        [HttpPost("blocked-apps")]
        public async Task<ActionResult<BlockedAppDto>> AddBlockedAppAsync([FromBody] BlockedAppUpsertRequest request)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.BlockValue))
            {
                return BadRequest("Block value cannot be empty.");
            }

            var result = await _repository.AddBlockedAppAsync(request.BlockType, request.BlockValue.Trim());

            if (result is null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Failed to create blocked app.");
            }

            return Created($"/api/blocked-apps/{result.Id}", result);
        }

        [HttpPut("blocked-apps/{id:int}")]
        public async Task<ActionResult<BlockedAppDto>> UpdateBlockedAppAsync(int id, [FromBody] BlockedAppUpsertRequest request)
        {
            if (request is null)
            {
                return BadRequest("Request body is required.");
            }

            if (string.IsNullOrWhiteSpace(request.BlockValue))
            {
                return BadRequest("Block value cannot be empty.");
            }

            var result = await _repository.UpdateBlockedAppAsync(id, request.BlockType, request.BlockValue.Trim());

            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        [HttpDelete("blocked-apps/{id:int}")]
        public async Task<IActionResult> DeleteBlockedAppAsync(int id)
        {
            var deleted = await _repository.DeleteBlockedAppAsync(id);

            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
