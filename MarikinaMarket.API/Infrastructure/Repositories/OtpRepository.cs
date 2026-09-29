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

        public async Task<OtpVerification?> GetActiveOtpAsync(int userId, OtpPurpose purpose)
        {
            return await _context.OtpVerifications
                .Where(o => o.UserId == userId
                    && o.Purpose == purpose
                    && o.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task InvalidatePriorOtpsAsync(int userId, OtpPurpose purpose)
        {
            await _context.OtpVerifications
                .Where(o => o.UserId == userId && o.Purpose == purpose && (o.IsUsed || o.ExpiresAt <= DateTime.UtcNow))
                .ExecuteDeleteAsync();
        }

        public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    }
}