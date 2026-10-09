using MarikinaMarket.API.Application.DTOs.Ordinance.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.DTOs.SystemConfiguration.Response;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Application.DTOs.Ordinance.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdinanceController : ControllerBase
    {
        private readonly IOrdinanceService _service;

        public OrdinanceController(IOrdinanceService service) => _service = service;

        [HttpGet]
        [Authorize(Roles = $"{nameof(Role.HeadAdmin)},{nameof(Role.AdminOfficer)},{nameof(Role.MarketEnforcer)}")]
        public async Task<ActionResult<List<OrdinanceSummaryResponse>>> LoadOrdinances()
        {
            var ordinances = await _service.GetOrdinances();

            if (!ordinances.Any())
                return NotFound("Ordinance list not found.");

            return Ok(ordinances);
        }

        [HttpGet("{id}")]
        [Authorize(Roles = $"{nameof(Role.HeadAdmin)},{nameof(Role.AdminOfficer)}")]
        public async Task<ActionResult<OrdinanceDetailResponse>> GetById(int id)
            => Ok(await _service.GetByIdAsync(id));

        [HttpPost]
        [Authorize(Roles = $"{nameof(Role.HeadAdmin)},{nameof(Role.AdminOfficer)}")]
        public async Task<ActionResult<OrdinanceSummaryResponse>> Create([FromBody] SaveOrdinanceRequest request)
        {
            var response = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = $"{nameof(Role.HeadAdmin)},{nameof(Role.AdminOfficer)}")]
        public async Task<ActionResult<OrdinanceSummaryResponse>> Update(int id, [FromBody] SaveOrdinanceRequest request)
            => Ok(await _service.UpdateAsync(id, request));

        [HttpGet("count")]
        [Authorize(Roles = $"{nameof(Role.HeadAdmin)},{nameof(Role.AdminOfficer)},{nameof(Role.MarketEnforcer)}")]
        public async Task<ActionResult<CountResponse>> GetActiveCount()
            => Ok(await _service.GetActiveCountAsync());
    }
}
