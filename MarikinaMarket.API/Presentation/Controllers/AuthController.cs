using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MarikinaMarket.API.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;

        public AuthController(IUserService userService)
        {
            _userService = userService;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (request == null)
                    return Unauthorized("Request data must not be null or empty.");

                return await _userService.LoginAsync(request);
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [AllowAnonymous]
        [HttpPost("login-mobile")]
        public async Task<ActionResult<LoginMobileResponse>> LoginMobile([FromBody] LoginRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Request data must not be null or empty.");

                return await _userService.LoginMobileAsync(request);
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Request data must not be null or empty.");

                return await _userService.RegisterAsync(request);
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<ActionResult<TokenRefreshResponse>> RefreshTokens([FromBody] TokenRefreshRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Request data must not be null or empty.");

                return await _userService.RefreshTokens(request);
            }
            catch (Exception ex)
            {
                return Unauthorized(ex.Message);
            }
        }
    }
}
