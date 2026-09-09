using MarikinaMarket.API.Application.DTOs.Enforcers.Request;
using MarikinaMarket.API.Application.DTOs.Enforcers.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/admin/enforcers")]
    [Authorize(Roles = nameof(Role.Admin))]
    public class AdminEnforcerController : ControllerBase
    {
        private readonly IEnforcerService _enforcerService;
        private readonly ITicketService _ticketService;

        public AdminEnforcerController(
            IEnforcerService enforcerService, 
            ITicketService ticketService,
            IUserService userService) 
        {
            _enforcerService = enforcerService;
            _ticketService = ticketService;
        }

        [HttpGet]
        public async Task<ActionResult<PageResponse<AdminEnforcerSummaryResponse>>> GetEnforcerSummary(
            [FromQuery] EnforcerSummaryFilter filters,
            [FromQuery] int offset = 0
        ) => Ok(await _enforcerService.GetEnforcerSummaryAsync(offset, filters));

        [HttpGet("activities")]
        public async Task<ActionResult> GetAverageTickets()
            => Ok(await _enforcerService.GetDailyTicketsAverage());

        [HttpGet("profile/{enforcerId}")]
        public async Task<ActionResult<EnforcerInfoResponse>> GetEnforcerInfo([FromRoute] int enforcerId)
        {
            if (enforcerId <= 0)
                return BadRequest("Enforcer identification must not be empty.");

            return Ok(await _enforcerService.GetProfileAsync(enforcerId));
        }

        [HttpGet("performance-summary/{enforcerid}")]
        public async Task<ActionResult<PerformanceSummaryResponse>> GetPerformanceSummary([FromRoute] int enforcerId)
        {
            if (enforcerId <= 0)
                return BadRequest("Enforcer identification must not be empty.");

            return Ok(await _enforcerService.GetPerformanceSummaryAsync(enforcerId));
        }

        [HttpGet("{enforcerId}/inspections/history")]
        public async Task<ActionResult<PageResponse<InspectionResponse>>> GetInspectionHistory([FromRoute] int enforcerId, [FromQuery] int offset = 0)
        {
            if (enforcerId <= 0)
                return BadRequest("Enforcer identification must not be empty.");

            return Ok(await _enforcerService.GetInspectionHistoryAsync(enforcerId, offset));
        }
    }
}