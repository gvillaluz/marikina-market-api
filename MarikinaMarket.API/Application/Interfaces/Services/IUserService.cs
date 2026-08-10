using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;
using MarikinaMarket.API.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IUserService
    {
        public Task<LoginResponse> LoginAsync(LoginRequest request);
        public Task<LoginMobileResponse> LoginMobileAsync(LoginRequest request);
        public Task<RegisterResponse> RegisterAsync(RegisterRequest request);
        public Task<User?> ValidateCredentialsAsync(string userName, string password);
        public Task<TokenRefreshResponse> RefreshTokensAsync(TokenRefreshRequest request);
        public Task<UserProfileResponse> GetUserInfoAsync(int userId);
        public Task<UserProfileResponse> UpdateUserInfo(EditUserRequest request, int userId);
        public Task<IdentityResult> MandatoryChangePasswordAsync(ChangePasswordRequest request, int userId);
        public Task<IdentityResult> ChangePasswordAsync(ChangePasswordRequest request, int userId, bool clearMandatoryFlag = false);
    }
}
