using MarikinaMarket.API.Application.DTOs.Auth.Request;
using MarikinaMarket.API.Application.DTOs.Auth.Response;
using Microsoft.AspNetCore.Identity;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task<LoginMobileResponse> LoginMobileAsync(LoginRequest request);
        Task<RegisterResponse> RegisterAsync(RegisterRequest request);
        Task<TokenRefreshResponse> RefreshTokensAsync(TokenRefreshRequest request);
        Task<SendCodeResponse> SendCodeAsync(SendCodeRequest request);
        Task<VerifyCodeResponse> VerifyCodeAsync(VerifyCodeRequest request);
        Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request);
        Task<IdentityResult> MandatoryChangePasswordAsync(ChangePasswordRequest request, int userId);
        Task<IdentityResult> ChangePasswordAsync(ChangePasswordRequest request, int userId, bool clearMandatoryFlag = false);
        Task<FindAccountResponse> FindAccountAsync(FindAccountRequest request);
    }
}