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
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IUserService _service;
        
        public UserController(IUserService service) => _service = service;

        [HttpPost("profile")]
        public async Task<ActionResult<UserProfileResponse>> EditUserInformation([FromBody] EditUserRequest request)
        {
            var userId = GetUserIdFromClaims();
            var userProfile = await _service.UpdateUserInfo(request, userId);

            return Ok(userProfile);
        }

        [HttpPost("me")]
        public async Task<ActionResult<UserProfileResponse>> GetLoggedInUserInfo()
        {
            var userId = GetUserIdFromClaims();

            var userProfile = await _service.GetUserInfoAsync(userId);

            return Ok(userProfile);
        }

        [HttpPost("device-token")]
        [Authorize(Roles = nameof(Role.Enforcer))]
        public async Task<IActionResult> RegisterDeviceToken([FromBody] RegisterDeviceTokenRequest request)
        {
            var userId = GetUserIdFromClaims();
            await _service.RegisterDeviceTokenAsync(userId, request.DeviceToken);
            return Ok(new { message = "Device token registered." });
        }

        [HttpPut("change-photo")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<UserProfileResponse>> UpdateProfilePicture(
            [FromForm] IFormFile file)
        {
            var userId = GetUserIdFromClaims();
            return Ok(await _service.ChangeProfilePhoto(userId, file));
        }

        [HttpPut("remove-photo")]
        public async Task<ActionResult<UserProfileResponse>> RemoveProfilePhoto() 
        {
            var userId = GetUserIdFromClaims();
            return Ok(await _service.RemoveProfilePhoto(userId));
        }

        private int GetUserIdFromClaims()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdString == null || !int.TryParse(userIdString, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid or missing identity token context.");
            }
            return userId;
        }
    }
}