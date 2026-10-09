using Microsoft.AspNetCore.Mvc;
using MarikinaMarket.API.Application.DTOs.MarketSection.Response;
using MarikinaMarket.API.Application.DTOs.MarketSection.Request;
using MarikinaMarket.API.Application.DTOs.SystemConfiguration.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MarketSectionController : ControllerBase
    {
        private readonly IMarketSectionService _service;

        public MarketSectionController(IMarketSectionService service) => _service = service;

        [HttpGet]
        public async Task<ActionResult<List<MarketSectionResponse>>> GetAll()
            => Ok(await _service.GetAllAsync());

        [HttpPost]
        [Authorize(Roles = $"{nameof(Role.HeadAdmin)},{nameof(Role.AdminOfficer)}")]
        public async Task<ActionResult<MarketSectionResponse>> Create([FromBody] SaveMarketSectionRequest request)
        {
            var response = await _service.CreateAsync(request);
            return StatusCode(StatusCodes.Status201Created, response);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = $"{nameof(Role.HeadAdmin)},{nameof(Role.AdminOfficer)}")]
        public async Task<ActionResult<MarketSectionResponse>> Update(int id, [FromBody] SaveMarketSectionRequest request)
            => Ok(await _service.UpdateAsync(id, request));

        [HttpGet("count")]
        public async Task<ActionResult<CountResponse>> GetActiveCount()
            => Ok(await _service.GetActiveCountAsync());

        [HttpPatch("{id}/active")]
        [Authorize(Roles = $"{nameof(Role.HeadAdmin)},{nameof(Role.AdminOfficer)}")]
        public async Task<IActionResult> UpdateActiveStatus(
            int id,
            [FromBody] UpdateMarketSectionActiveRequest request)
        {
            await _service.UpdateActiveStatusAsync(id, request.IsActive);
            return NoContent();
        }
    }
}
