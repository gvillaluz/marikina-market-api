using MarikinaMarket.API.Application.DTOs.Enforcers.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;
using MarikinaMarket.API.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IUserService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task<LoginMobileResponse> LoginMobileAsync(LoginRequest request);
        Task<RegisterResponse> RegisterAsync(RegisterRequest request);
        Task<User?> ValidateCredentialsAsync(string userName, string password);
        Task<TokenRefreshResponse> RefreshTokensAsync(TokenRefreshRequest request);
        Task<UserProfileResponse> GetUserInfoAsync(int userId);
        Task<UserProfileResponse> UpdateUserInfo(EditUserRequest request, int userId);
        Task<IdentityResult> MandatoryChangePasswordAsync(ChangePasswordRequest request, int userId);
        Task<IdentityResult> ChangePasswordAsync(ChangePasswordRequest request, int userId, bool clearMandatoryFlag = false);
        Task RegisterDeviceTokenAsync(int userId, string deviceToken);
        Task<UserProfileResponse> ChangeProfilePhoto(int userId, IFormFile file);
        Task<UserProfileResponse> RemoveProfilePhoto(int userId);
    }
}
