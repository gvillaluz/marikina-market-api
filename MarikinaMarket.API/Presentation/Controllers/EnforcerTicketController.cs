using System.Security.Claims;
using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/enforcer/tickets")]
    [Authorize(Roles = nameof(Role.Enforcer))]
    public class EnforcerTicketController : ControllerBase
    {
        private readonly ITicketService _service;

        public EnforcerTicketController(ITicketService service) => _service = service;

        [HttpGet("dashboard")]
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
        public async Task<ActionResult<TicketDetailResponse>> GetTicketById([FromRoute] int id)
        {
            if (id <= 0)
                return BadRequest("Ticket identification must not be empty.");

            return Ok(await _service.GetTicketDetailByIdAsync(id));
        }

        [HttpPost("new-inspection")]
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

        [HttpGet("inspections")]
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

        [HttpGet]
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
        public async Task<ActionResult<FineSummaryResponse>> GetTicketFineSummary([FromBody] FineSummaryRequest request)
        {
            if (!request.OrdinanceIds.Any() || request.VendorId <= 0)
                return BadRequest("Ordinance and vendor must not be empty.");

            return Ok(await _service.GetOffenseCountsAndPaymentBy(request.OrdinanceIds, request.VendorId));
        }
    }
}