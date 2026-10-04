using MarikinaMarket.API.Domain.Entities;
using System.Security.Claims;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface ITokenService
    {
        public Task<string> GenerateAccessToken(User user);
        public Task<ClaimsPrincipal?> ValidateAccessTokenAsync(string accessToken);
        public RefreshToken GenerateRefreshToken(int userId);
    }
}
