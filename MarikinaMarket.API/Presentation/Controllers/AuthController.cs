using MarikinaMarket.API.Application.DTOs.Auth.Request;
using MarikinaMarket.API.Application.DTOs.Auth.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _service;

        public AuthController(IAuthService service) => _service = service;

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<SendCodeResponse>> Login([FromBody] LoginRequest request)
        {
            if (request == null)
                return BadRequest("Request data must not be null or empty.");

            return Ok(await _service.LoginAsync(request));
        }

        [AllowAnonymous]
        [HttpPost("login-mobile")]
        public async Task<ActionResult<SendCodeResponse>> LoginMobile([FromBody] LoginRequest request)
        {
            if (request == null)
                return BadRequest("Request data must not be null or empty.");

            return Ok(await _service.LoginMobileAsync(request));
        }

        [AllowAnonymous]
        [HttpPost("verify-login")]
        public async Task<ActionResult<LoginResponse>> VerifyLogin([FromBody] LoginVerificationRequest request)
            => Ok(await _service.VerifyLoginAsync(request));

        [AllowAnonymous]
        [HttpPost("verify-login-mobile")]
        public async Task<ActionResult<LoginMobileResponse>> VerifyLoginMobile([FromBody] LoginVerificationRequest request)
            => Ok(await _service.VerifyLoginMobileAsync(request));

        [Authorize(Roles = nameof(Role.HeadAdmin))]
        [HttpPost("register")]
        public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
        {
            if (request == null)
                return BadRequest("Request data must not be null or empty.");

            return Ok(await _service.RegisterAsync(request));
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<ActionResult<TokenRefreshResponse>> RefreshTokens([FromBody] TokenRefreshRequest request)
        {
            if (request == null)
                return BadRequest("Request data must not be null or empty.");

            return Ok(await _service.RefreshTokensAsync(request));
        }

        [AllowAnonymous]
        [HttpPost("refresh-web")]
        public async Task<ActionResult<LoginResponse>> RefreshAccessToken([FromBody] AccessTokenRefreshRequest request)
        {
            if (request == null)
                return BadRequest("Request data must not be null or empty.");

            return Ok(await _service.RefreshAccessTokenAsync(request));
        }

        [HttpPost("mandatory-change-password")]
        public async Task<ActionResult> MandatoryChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = GetUserIdFromClaims();

            var result = await _service.MandatoryChangePasswordAsync(request, userId);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { code = "PASSWORD_CHANGE_FAILED", message = errors });
            }

            return Ok();
        }

        [HttpPost("change-password")]
        public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = GetUserIdFromClaims();
            var result = await _service.ChangePasswordAsync(request, userId);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest(new { code = "PASSWORD_CHANGE_FAILED", message = errors });
            }

            return Ok();
        }

        [AllowAnonymous]
        [HttpPost("find-account")]
        public async Task<ActionResult<FindAccountResponse>> FindAccount([FromBody] FindAccountRequest request)
        {
            if (string.IsNullOrEmpty(request.Username)) 
                return BadRequest("Request body must not be empty.");

            return Ok(await _service.FindAccountAsync(request));
        }

        [AllowAnonymous]
        [HttpPost("send-otp")]
        public async Task<ActionResult<SendCodeResponse>> SendOtpCode([FromBody] SendCodeRequest request)
        {
            return Ok(await _service.SendCodeAsync(request));
        }

        [AllowAnonymous]
        [HttpPost("verify-otp")]
        public async Task<ActionResult<VerifyCodeResponse>> VerifyCode([FromBody] VerifyCodeRequest request) 
            => Ok(await _service.VerifyCodeAsync(request));

        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<ActionResult<ResetPasswordResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
            => Ok(await _service.ResetPasswordAsync(request));

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
