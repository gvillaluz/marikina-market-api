using MarikinaMarket.API.Application;
using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;

        public AuthController(IUserService userService) => _userService = userService;

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (request == null)
                return Unauthorized("Request data must not be null or empty.");

            return Ok(await _userService.LoginAsync(request));
        }

        [AllowAnonymous]
        [HttpPost("login-mobile")]
        public async Task<ActionResult<LoginMobileResponse>> LoginMobile([FromBody] LoginRequest request)
        {
            if (request == null)
                return BadRequest("Request data must not be null or empty.");

            return Ok(await _userService.LoginMobileAsync(request));
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
        {
            if (request == null)
                return BadRequest("Request data must not be null or empty.");

            return Ok(await _userService.RegisterAsync(request));
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<ActionResult<TokenRefreshResponse>> RefreshTokens([FromBody] TokenRefreshRequest request)
        {
            if (request == null)
                return BadRequest("Request data must not be null or empty.");

            return Ok(await _userService.RefreshTokensAsync(request));
        }

        [Authorize]
        [HttpPost("profile")]
        public async Task<ActionResult<UserProfileResponse>> EditUserInformation([FromBody] EditUserRequest request)
        {
            var userId = GetUserIdFromClaims();
            var userProfile = await _userService.UpdateUserInfo(request, userId);

            return Ok(userProfile);
        }

        [Authorize]
        [HttpPost("me")]
        public async Task<ActionResult<UserProfileResponse>> GetLoggedInUserInfo()
        {
            var userId = GetUserIdFromClaims();

            var userProfile = await _userService.GetUserInfoAsync(userId);

            return Ok(userProfile);
        }

        [Authorize]
        [HttpPost("mandatory-change-password")]
        public async Task<IActionResult> MandatoryChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = GetUserIdFromClaims();

            var result = await _userService.MandatoryChangePasswordAsync(request, userId);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { code = "PASSWORD_CHANGE_FAILED", message = errors });
            }

            return NoContent();
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = GetUserIdFromClaims();
            var result = await _userService.ChangePasswordAsync(request, userId);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { code = "PASSWORD_CHANGE_FAILED", message = errors });
            }

            return NoContent();
        }

        [HttpPost("device-token")]
        [Authorize(Roles = nameof(Role.Enforcer))]
        public async Task<IActionResult> RegisterDeviceToken([FromBody] string deviceToken)
        {
            var userId = GetUserIdFromClaims();
            await _userService.RegisterDeviceTokenAsync(userId, deviceToken);
            return Ok(new { message = "Device token registered." });
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
