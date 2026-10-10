using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Application.DTOs.Audits.Request;
using MarikinaMarket.API.Application.DTOs.Audits.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/admin/audit-logs")]
    [Authorize(Roles = $"{nameof(Role.HeadAdmin)},{nameof(Role.AdminOfficer)}")]
    public class AdminAuditLogController : ControllerBase
    {
        private readonly IAuditLogService _service;
        public AdminAuditLogController(IAuditLogService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<PageResponse<AuditLogSummaryResponse>>> GetSummaries(
            [FromQuery] AuditLogFilter filters, [FromQuery, Range(0, int.MaxValue)] int offset = 0,
            CancellationToken cancellationToken = default)
            => Ok(await _service.GetSummariesAsync(offset, filters, cancellationToken));

        [HttpGet("counts")]
        public async Task<ActionResult<AuditLogCountsResponse>> GetCounts([FromQuery] AuditLogFilter filters,
            CancellationToken cancellationToken = default)
            => Ok(await _service.GetActivityCountsAsync(filters, cancellationToken));

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AuditLogDetailResponse>> GetDetails(int id, CancellationToken cancellationToken)
            => Ok(await _service.GetDetailsAsync(id, cancellationToken));
    }
}
