using MarikinaMarket.API.Application.DTOs.Ordinance.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
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
        [Authorize(Roles = $"{nameof(Role.Admin)},{nameof(Role.Enforcer)}")]
        public async Task<ActionResult<List<GetOrdinanceResponse>>> LoadOrdinances()
        {
            var ordinances = await _service.GetOrdinances();

            if (!ordinances.Any())
                return NotFound("Ordinance list not found.");

            return Ok(ordinances);
        }
    }
}
