using System.Security.Claims;
using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Roles = nameof(Role.HeadAdmin))]
    public class AdminUserController : ControllerBase
    {
        private readonly IAdminUserService _service;

        public AdminUserController(IAdminUserService service) => _service = service;

        [HttpPost]
        public async Task<ActionResult<AdminUserResponse>> CreateUser([FromBody] CreateAdminUserRequest request)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId) || adminId <= 0)
                return Unauthorized();

            var response = await _service.CreateUserAsync(request, adminId);
            return StatusCode(StatusCodes.Status201Created, response);
        }
    }
}
