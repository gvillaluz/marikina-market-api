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
        private readonly IEnforcerService _service;

        public AdminEnforcerController(IEnforcerService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<PageResponse<AdminEnforcerSummaryResponse>>> GetEnforcerSummary(
            [FromQuery] EnforcerSummaryFilter filters,
            [FromQuery] int offset = 0
        ) => Ok(await _service.GetEnforcerSummaryAsync(offset, filters));

        [HttpGet("activities")]
        public async Task<ActionResult> GetAverageTickets()
            => Ok(await _service.GetDailyTicketsAverage());
    }
}