using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/admin/accounts")]
    [Authorize(Roles = nameof(Role.HeadAdmin))]
    public class AdminAccountController : ControllerBase
    {
        private readonly IUserService _service;

        public AdminAccountController(IUserService service) => _service = service;

        [HttpGet("counts")]
        public async Task<ActionResult<AccountCountsResponse>> GetCounts()
            => Ok(await _service.GetAccountCountsAsync());

        [HttpGet]
        public async Task<ActionResult<PageResponse<UserSummaryResponse>>> GetUserSummaries(
            [FromQuery] UserSummaryFilter filters,
            [FromQuery, Range(0, int.MaxValue)] int offset = 0)
            => Ok(await _service.GetUserSummariesAsync(offset, filters));
    }
}
