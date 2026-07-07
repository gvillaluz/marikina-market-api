using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface ITokenService
    {
        public Task<string> GenerateAccessToken(User user);
        public RefreshToken GenerateRefreshToken(int userId);
    }
}
