using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface IOtpRepository
    {
        Task<OtpVerification> AddOtpAsync(OtpVerification otp);
        Task<OtpVerification?> GetLatestOtpAsync(int userId, OtpPurpose purpose);
        Task InvalidatePriorOtpsAsync(int userId, OtpPurpose purpose);
        Task<bool> TryUseOtpAsync(int otpId, int maxAttempts);
        Task IncrementAttemptsAsync(int otpId, int maxAttempts);
        Task SaveChangesAsync();
    }
}
