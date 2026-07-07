using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface IUserRepository
    {
        public Task<User?> FindByUserNameAsync(string userName);
        public Task<User?> FindByEmailAsync(string email);
        public Task<IdentityResult> CreateUserAsync(User user, string password);
        public Task<IdentityResult> CreateUserWithPassAsync(User user);
        public Task<SignInResult> CheckPasswordAsync(User user, string password);
        public Task AddToRoleAsync(User user, string role);
        public Task<Role?> GetRoleAsync(User user);
        public Task<RefreshToken> AddRefreshTokenAsync(RefreshToken refreshToken);
        public Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken);
        public Task<string> GetNextUserNameAsync();
        public Task SaveChangesAsync();
    }
}
