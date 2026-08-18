using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TicketController : ControllerBase
    {
        private readonly ITicketService _service;
        public TicketController(ITicketService service) => _service = service;

        [HttpGet("enforcer/dashboard")]
        [Authorize(Roles = nameof(Role.Enforcer))]
        public async Task<ActionResult<MobileDashboardSummaryResponse>> GetMobileDashboardSummary()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized("Invalid token.");

            int enforcerId = int.Parse(userIdClaim);

            return Ok(await _service.GetMobileTicketCountAsync(enforcerId));
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetTicketById([FromRoute] int id)
        {
            if (id <= 0)
                return BadRequest("Ticket identification must not be empty.");

            return Ok(await _service.GetTicketDetailByIdAsync(id));
        }

        [HttpPost("new-inspection")]
        [Authorize(Roles = nameof(Role.Enforcer))]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<InspectionSummaryResponse>> SaveNewInspection([FromForm] CreateTicketRequest request)
        {
            if (request == null) return BadRequest("Request body must not be null or empty.");

            foreach (var file in request.TicketEvidenceFiles)
            {
                Console.WriteLine($"FILE: {file.FileName}");
            }

            var ticketResponse = await _service.CreateTicketAsync(request);

            return CreatedAtAction(nameof(GetTicketById), new { id = ticketResponse.Id }, ticketResponse);  
        }

        [HttpGet("enforcer/inspections")]
        [Authorize(Roles = nameof(Role.Enforcer))]
        public async Task<ActionResult<PageResponse<InspectionSummaryResponse>>> GetAllInspectionsByEnforcerId(
            [FromQuery] int offset = 0,
            [FromQuery] ViolationType type = ViolationType.Warning
            )
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized("Invalid token.");

            int enforcerId = int.Parse(userIdClaim);

            return Ok(await _service.GetInspectionsByEnforcerIdAsync(enforcerId, offset, type));
        }

        [HttpGet("enforcer/tickets")]
        [Authorize(Roles = nameof(Role.Enforcer))]
        public async Task<ActionResult<PageResponse<TicketSummaryResponse>>> GetAllTicketsByEnforcerId(
            [FromQuery] int offset = 0,
            [FromQuery] TicketStatus status = TicketStatus.Pending
            )
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized("Invalid token.");

            int enforcerId = int.Parse(userIdClaim);

            return Ok(await _service.GetTicketsByEnforcerIdAsync(enforcerId, offset, status));
        }

        [HttpPost("fine-summary")]
        [Authorize(Roles = nameof(Role.Enforcer))]
        public async Task<ActionResult<FineSummaryResponse>> GetTicketFineSummary([FromBody] FineSummaryRequest request)
        {
            if (!request.OrdinanceIds.Any() || request.VendorId <= 0)
                return BadRequest("Ordinance and vendor must not be empty.");

            return Ok(await _service.GetOffenseCountsAndPaymentBy(request.OrdinanceIds, request.VendorId));
        }

        [HttpPatch("{ticketId}/update-status")]
        [Authorize(Roles = nameof(Role.Admin))]
        public async Task<ActionResult<UpdateStatusResponse>> UpdateTicketStatus([FromRoute] int ticketId, [FromBody] UpdateStatusRequest request)
        {
            return Ok(await _service.UpdateTicketStatusAsync(ticketId, request));
        }
    }
}