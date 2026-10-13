using System.ComponentModel.DataAnnotations;
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
    [Route("api/vendor/tickets")]
    [Authorize(Roles = nameof(Role.MarketVendor))]
    public class VendorTicketController : ControllerBase
    {
        private readonly ITicketService _service;

        public VendorTicketController(ITicketService service) => _service = service;

        [HttpGet("{id}")]
        public async Task<ActionResult<TicketDetailResponse>> GetTicketDetail([FromRoute] int id)
        {
            if (id <= 0)
                return BadRequest("Ticket identification must be greater than zero.");

            if (!TryGetUserId(out var userId))
                return Unauthorized("Invalid token.");

            return Ok(await _service.GetVendorTicketDetailAsync(id, userId));
        }

        [HttpGet("inspections")]
        public async Task<ActionResult<PageResponse<VendorInspectionSummaryResponse>>> GetInspections(
            [FromQuery] VendorInspectionFilters filters,
            [FromQuery, Range(0, int.MaxValue, ErrorMessage = "Offset must not be negative.")] int offset = 0)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("Invalid token.");

            return Ok(await _service.GetVendorInspectionsAsync(userId, offset, filters));
        }

        [HttpGet("{id}/settlement/receipt")]
        public async Task<ActionResult<TicketReceiptProofResponse>> GetReceiptProof([FromRoute] int id)
        {
            if (id <= 0)
                return BadRequest("Ticket identification must be greater than zero.");
            if (!TryGetUserId(out var userId))
                return Unauthorized("Invalid token.");

            return Ok(await _service.GetVendorTicketReceiptProofAsync(id, userId));
        }

        [HttpPost("{id}/settlement/receipt")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<TicketReceiptProofResponse>> SubmitReceiptProof(
            [FromRoute] int id, [FromForm] SubmitTicketReceiptProofRequest request)
        {
            if (id <= 0)
                return BadRequest("Ticket identification must be greater than zero.");
            if (!TryGetUserId(out var userId))
                return Unauthorized("Invalid token.");

            return Ok(await _service.SubmitVendorTicketReceiptProofAsync(id, userId, request));
        }

        [HttpGet("{id}/settlement/community-service")]
        public async Task<ActionResult<CommunityServiceProgressResponse>> GetCommunityServiceProgress([FromRoute] int id)
        {
            if (id <= 0)
                return BadRequest("Ticket identification must be greater than zero.");
            if (!TryGetUserId(out var userId))
                return Unauthorized("Invalid token.");

            return Ok(await _service.GetVendorCommunityServiceProgressAsync(id, userId));
        }

        private bool TryGetUserId(out int userId)
            => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId) && userId > 0;
    }
}
