using MarikinaMarket.API.Application.DTOs.Auth.Request;
using MarikinaMarket.API.Application.DTOs.Auth.Response;
using Microsoft.AspNetCore.Identity;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IAuthService
    {
        Task<SendCodeResponse> LoginAsync(LoginRequest request);
        Task<SendCodeResponse> LoginMobileAsync(LoginRequest request);
        Task<LoginResponse> VerifyLoginAsync(LoginVerificationRequest request);
        Task<LoginMobileResponse> VerifyLoginMobileAsync(LoginVerificationRequest request);
        Task<RegisterResponse> RegisterAsync(RegisterRequest request);
        Task<TokenRefreshResponse> RefreshTokensAsync(TokenRefreshRequest request);
        Task<LoginResponse> RefreshAccessTokenAsync(AccessTokenRefreshRequest request);
        Task<SendCodeResponse> SendCodeAsync(SendCodeRequest request);
        Task<VerifyCodeResponse> VerifyCodeAsync(VerifyCodeRequest request);
        Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request);
        Task<IdentityResult> MandatoryChangePasswordAsync(ChangePasswordRequest request, int userId);
        Task<IdentityResult> ChangePasswordAsync(ChangePasswordRequest request, int userId, bool clearMandatoryFlag = false);
        Task<FindAccountResponse> FindAccountAsync(FindAccountRequest request);
    }
}
