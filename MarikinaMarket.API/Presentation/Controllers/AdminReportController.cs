using System.Security.Claims;
using MarikinaMarket.API.Application.DTOs.Reports.Request;
using MarikinaMarket.API.Application.DTOs.Reports.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/admin/reports")]
    [Authorize(Roles = $"{nameof(Role.HeadAdmin)},{nameof(Role.AdminOfficer)}")]
    public class AdminReportController : ControllerBase
    {
        private readonly IReportService _service;
        public AdminReportController(IReportService service) => _service = service;

        [HttpGet("preview")]
        public async Task<ActionResult<ReportPreviewResponse>> Preview([FromQuery] string? date,
            [FromQuery] string type = "Warning & Ticket") => Ok(await _service.PreviewAsync(date, type));

        [HttpPost]
        public async Task<ActionResult<ReportDetailResponse>> Generate([FromBody] GenerateReportRequest request)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId) || adminId <= 0)
                return Forbid();
            return Ok(await _service.GenerateAsync(adminId, request));
        }

        [HttpGet]
        public async Task<ActionResult<PageResponse<ReportSummaryResponse>>> History([FromQuery] int offset = 0)
            => Ok(await _service.GetHistoryAsync(offset));

        [HttpGet("{id}")]
        public async Task<ActionResult<ReportDetailResponse>> Detail([FromRoute] int id)
            => Ok(await _service.GetDetailAsync(id));
    }
}
