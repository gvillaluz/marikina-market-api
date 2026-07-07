using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TicketController : ControllerBase
    {
        private readonly ITicketService _service;
        public TicketController(ITicketService service)
        {
            _service = service;
        }

        [HttpGet("{id}")]
        public IActionResult GetTicketById(int id)
        {
            if (id <= 0)
                return NotFound(new { message = "TANGINA WALA" });

            return Ok(new { message = "TANGINA" });
        }

        [HttpPost]
        [Authorize(Roles = nameof(Role.Enforcer))]
        public async Task<ActionResult<TicketDetailResponse>> CreateTicket([FromBody] CreateTicketRequest request)
        {
            try
            {
                if (request == null) return BadRequest("Request body must not be null or empty.");

                var ticketResponse = await _service.CreateTicketAsync(request);

                return CreatedAtAction(nameof(GetTicketById), new { id = ticketResponse.Id }, ticketResponse);
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpGet("enforcer/tickets")]
        [Authorize(Roles = nameof(Role.Enforcer))]
        public async Task<ActionResult<List<TicketDetailResponse>>> GetAllTicketsByEnforcerId()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userIdClaim))
                    return Unauthorized("Invalid token.");

                int enforcerId = int.Parse(userIdClaim);

                var tickets = await _service.GetAllByEnforcerId(enforcerId);

                return tickets;
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }
}
