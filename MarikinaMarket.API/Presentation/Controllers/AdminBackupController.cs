using MarikinaMarket.API.Application.DTOs.Backups.Request;
using MarikinaMarket.API.Application.DTOs.Backups.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/admin/backups")]
    [Authorize(Roles = $"{nameof(Role.HeadAdmin)},{nameof(Role.AdminOfficer)}")]
    public class AdminBackupController : ControllerBase
    {
        private readonly IBackupService _service;
        public AdminBackupController(IBackupService service) => _service = service;

        [HttpPost]
        public async Task<ActionResult<BackupResponse>> Create(CancellationToken cancellationToken)
            => Ok(await _service.CreateManualAsync(cancellationToken));

        [HttpGet]
        public async Task<ActionResult<PageResponse<BackupResponse>>> History(CancellationToken cancellationToken, [FromQuery] int offset = 0)
            => Ok(await _service.GetHistoryAsync(offset, cancellationToken));

        [HttpGet("schedule")]
        public async Task<ActionResult<BackupScheduleResponse>> Schedule(CancellationToken cancellationToken)
            => Ok(await _service.GetScheduleAsync(cancellationToken));

        [HttpPut("schedule")]
        public async Task<ActionResult<BackupScheduleResponse>> UpdateSchedule(
            [FromBody] UpdateBackupScheduleRequest request, CancellationToken cancellationToken)
            => Ok(await _service.UpdateScheduleAsync(request, cancellationToken));

        [HttpGet("health")]
        public async Task<ActionResult<BackupHealthResponse>> Health(CancellationToken cancellationToken)
            => Ok(await _service.GetHealthAsync(cancellationToken));

        [HttpGet("{id}/download")]
        public async Task<IActionResult> Download([FromRoute] int id, CancellationToken cancellationToken)
        {
            var file = await _service.DownloadAsync(id, cancellationToken);
            Response.RegisterForDispose(file.Owner);
            Response.Headers.CacheControl = "no-store";
            return File(file.Stream, "application/octet-stream", file.FileName);
        }
    }
}
