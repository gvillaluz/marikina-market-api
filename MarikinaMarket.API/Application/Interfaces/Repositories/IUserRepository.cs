using MarikinaMarket.API.Application.DTOs.Enforcers.Request;
using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface IUserRepository
    {
        Task<AccountCountsResponse> GetAccountCountsAsync();
        Task<List<UserSummaryResponse>> GetUserSummariesAsync(int offset, int limit, UserSummaryFilter filters);
        Task<int> GetUserSummariesCountAsync(UserSummaryFilter filters);
        Task<User?> FindByUserNameAsync(string userName);
        Task<User?> FindByEmailAsync(string email);
        Task<IdentityResult> CreateUserAsync(User user, string password);
        Task<IdentityResult> CreateUserWithPassAsync(User user);
        Task<SignInResult> CheckPasswordAsync(User user, string password);
        Task<IdentityResult> AddToRoleAsync(User user, string role);
        Task<bool> RoleExistsAsync(Role role);
        Task<Role?> GetRoleAsync(User user);
        Task<RefreshToken> AddRefreshTokenAsync(RefreshToken refresshToken);
        Task<RefreshToken?> GetRefreshTokenAsync(string refreshToken);
        Task RevokeRefreshTokensAsync(int userId);
        Task<string> GetNextUserNameAsync();
        Task<User?> GetUserAsync(int userId);
        Task<Dictionary<int, UserNamesResponse>> GetNamesByIdsAsync(IEnumerable<int> userIds,
            CancellationToken cancellationToken = default);
        Task<List<User>> GetEnforcersAsync(int offset, int limit, EnforcerSummaryFilter filters);
        Task<IdentityResult> UpdateUserAsync(User user);
        Task<IdentityResult> ChangePasswordAsync(User user, string oldPassword, string newPassword);
        Task<List<string>> GetDeviceTokensByIdAsync(int userId);
        Task<List<string>> GetAdminDeviceTokensAsync();
        Task<UserDeviceToken?> GetDeviceTokenByValueAsync(string deviceToken);
        Task AddDeviceTokenAsync(UserDeviceToken userDeviceToken);
        Task<string> GenerateResetPassTokenAsync(User user);
        Task<IdentityResult> ResetPasswordByUsernameAsync(User user, string resetToken, string newPassword);
        Task<int> GetEnforcersCountAsync(EnforcerSummaryFilter filters);
        Task SaveChangesAsync();
    }
}
