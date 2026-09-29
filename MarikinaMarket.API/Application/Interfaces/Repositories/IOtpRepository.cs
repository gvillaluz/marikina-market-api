using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface IOtpRepository
    {
        Task<OtpVerification> AddOtpAsync(OtpVerification otp);
        Task<OtpVerification?> GetActiveOtpAsync(int userId, OtpPurpose purpose);
        Task InvalidatePriorOtpsAsync(int userId, OtpPurpose purpose);
        Task SaveChangesAsync();
    }
}