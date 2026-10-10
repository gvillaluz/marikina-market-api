using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace MarikinaMarket.API.Application.Services
{
    public class TokenService : ITokenService
    {
        private const int ACCESS_TOKEN_REFRESH_WINDOW_DAYS = 60;
        private readonly IConfiguration _config;
        private readonly UserManager<User> _userManager;

        public TokenService(IConfiguration config, UserManager<User> userManager)
        {
            _config = config;
            _userManager = userManager;
        }

        public async Task<string> GenerateAccessToken(User user)
        {
            var secretKey = _config["Jwt:Secret"];
            if(string.IsNullOrEmpty(secretKey))
            {
                throw new InvalidOperationException("JWT Secret key is missing from configuration.");
            }

            var handler = new JsonWebTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            var roles = await _userManager.GetRolesAsync(user);

            var claims = new Dictionary<string, object>
            {
                [ClaimTypes.NameIdentifier] = user.Id,
                ["first_name"] = user.FirstName,
                ["last_name"] = user.LastName,   
                ["security_stamp"] = await _userManager.GetSecurityStampAsync(user),
            };

            if (roles.Any())
            {
                claims["role"] = roles.First();
            }

            return handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = _config["Jwt:Issuer"],
                Audience = _config["Jwt:Audience"],
                Claims = claims,
                Expires = DateTime.UtcNow.AddMinutes(15),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            });
        }

        public async Task<ClaimsPrincipal?> ValidateAccessTokenAsync(string accessToken)
        {
            var secretKey = _config["Jwt:Secret"];
            if (string.IsNullOrEmpty(secretKey))
                throw new InvalidOperationException("JWT Secret key is missing from configuration.");

            var tokenHandler = new JsonWebTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false,
                RequireExpirationTime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _config["Jwt:Issuer"],
                ValidAudience = _config["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.Zero
            };

            var result = await tokenHandler.ValidateTokenAsync(accessToken, validationParameters);
            if (!result.IsValid)
                return null;

            if (result.SecurityToken is not JsonWebToken token
                || token.ValidTo == DateTime.MinValue
                || DateTime.UtcNow - token.ValidTo > TimeSpan.FromDays(ACCESS_TOKEN_REFRESH_WINDOW_DAYS))
                return null;

            return new ClaimsPrincipal(result.ClaimsIdentity);
        }

        public RefreshToken GenerateRefreshToken(int userId)
        {
            var randomBytes = RandomNumberGenerator.GetBytes(64);

            return new RefreshToken
            {
                Token = Convert.ToBase64String(randomBytes),
                ExpiresAt = DateTime.UtcNow.AddDays(60),
                UserId = userId,
                IsRevoked = false,
            };
        }
    }
}
