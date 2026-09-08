using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/admin/tickets")]
    [Authorize(Roles = nameof(Role.Admin))]
    public class AdminTicketController : ControllerBase 
    {
        private readonly ITicketService _service;

        public AdminTicketController(ITicketService service) => _service = service;

        [HttpGet("{ticketId}")]
        public async Task<ActionResult<AdminTicketDetailResponse>> GetTicketDetailById([FromRoute] int ticketId)
            => Ok(await _service.GetAdminTicketDetailAsync(ticketId));

        [HttpPatch("{ticketId}/update-status")]
        public async Task<ActionResult<UpdateStatusResponse>> UpdateTicketStatus([FromRoute] int ticketId, [FromBody] UpdateStatusRequest request)
            => Ok(await _service.UpdateTicketStatusAsync(ticketId, request));

        [HttpGet("inspections")]
        public async Task<ActionResult<PageResponse<AdminInspectionSummaryResponse>>> GetInspectionSummary(
            [FromQuery] InspectionSummaryFilters filters,
            [FromQuery] int offset = 0
        )
            => Ok(await _service.GetAdminInspectionAsync(offset, filters));

        [HttpGet]
        public async Task<ActionResult<AdminTicketSummaryResponse>> GetTicketSummary(
            [FromQuery] TicketSummaryFilters filters,
            [FromQuery] int offset = 0
        ) {
            Console.WriteLine(Request.QueryString);
            return Ok(await _service.GetAdminTicketAsync(offset, filters));
        }

        [HttpGet("analytics")]
        public async Task<ActionResult<TicketAnalyticsResponse>> GetTicketAnalytics()
            => Ok(await _service.GetTicketAnalyticsAsync());
    }
}