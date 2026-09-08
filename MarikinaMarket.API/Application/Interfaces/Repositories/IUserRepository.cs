using MarikinaMarket.API.Application.DTOs.Enforcers.Request;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface IUserRepository
    {
        Task<User?> FindByUserNameAsync(string userName);
        Task<User?> FindByEmailAsync(string email);
        Task<IdentityResult> CreateUserAsync(User user, string password);
        Task<IdentityResult> CreateUserWithPassAsync(User user);
        Task<SignInResult> CheckPasswordAsync(User user, string password);
        Task<IdentityResult> AddToRoleAsync(User user, string role);
        Task<Role?> GetRoleAsync(User user);
        Task<RefreshToken> AddRefreshTokenAsync(RefreshToken refresshToken);
        Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken);
        Task<string> GetNextUserNameAsync();
        Task<User?> GetUserAsync(int userId);
        Task<List<User>> GetEnforcersAsync(int offset, int limit, EnforcerSummaryFilter filters);
        Task<IdentityResult> UpdateUserAsync(User user);
        Task<IdentityResult> ChangePasswordAsync(User user, string oldPassword, string newPassword);
        Task<List<string>> GetDeviceTokensByIdAsync(int userId);
        Task<UserDeviceToken?> GetDeviceTokenByValueAsync(string deviceToken);
        Task AddDeviceTokenAsync(UserDeviceToken userDeviceToken);

        Task<int> GetEnforcersCountAsync(EnforcerSummaryFilter filters);
        Task SaveChangesAsync();
    }
}
