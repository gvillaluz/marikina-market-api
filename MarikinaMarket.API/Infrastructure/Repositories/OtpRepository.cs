using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarikinaMarket.API.Infrastructure.Repositories
{
    public class OtpRepository : IOtpRepository
    {
        private readonly AppDbContext _context;
        public OtpRepository(AppDbContext context) => _context = context;

        public async Task<OtpVerification> AddOtpAsync(OtpVerification otp)
        {
            await _context.OtpVerifications.AddAsync(otp);
            return otp;
        }

        public async Task<OtpVerification?> GetLatestOtpAsync(int userId, OtpPurpose purpose)
        {
            return await _context.OtpVerifications
                .AsNoTracking()
                .Where(o => o.UserId == userId
                    && o.Purpose == purpose)
                .OrderByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.Id)
                .FirstOrDefaultAsync();
        }

        public async Task InvalidatePriorOtpsAsync(int userId, OtpPurpose purpose)
        {
            await _context.OtpVerifications
                .Where(o => o.UserId == userId && o.Purpose == purpose)
                .ExecuteDeleteAsync();
        }

        public async Task<bool> TryUseOtpAsync(int otpId, int maxAttempts)
        {
            var updated = await _context.OtpVerifications
                .Where(o => o.Id == otpId && !o.IsUsed && o.Attempts < maxAttempts && o.ExpiresAt > DateTime.UtcNow)
                .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.IsUsed, true));

            return updated == 1;
        }

        public async Task IncrementAttemptsAsync(int otpId, int maxAttempts)
        {
            await _context.OtpVerifications
                .Where(o => o.Id == otpId && !o.IsUsed && o.Attempts < maxAttempts && o.ExpiresAt > DateTime.UtcNow)
                .ExecuteUpdateAsync(setters => setters.SetProperty(o => o.Attempts, o => o.Attempts + 1));
        }

        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }
}
