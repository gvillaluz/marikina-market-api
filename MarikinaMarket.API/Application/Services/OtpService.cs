using System.Security.Cryptography;
using System.Text;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Services
{
    public class OtpService : IOtpService
    {
        private readonly IOtpRepository _otpRepository;
        private const int MaxAttempts = 5;

        public OtpService(IOtpRepository otpRepository) => _otpRepository = otpRepository;

        public async Task<string> GenerateOtpAsync(int userId, OtpPurpose purpose, int expiryMinutes = 5)
        {
            await _otpRepository.InvalidatePriorOtpsAsync(userId, purpose);

            var code = GenerateSecureCode();

            var otp = new OtpVerification
            {
                UserId = userId,
                CodeHash = HashCode(code),
                Purpose = purpose,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes),
                Attempts = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _otpRepository.AddOtpAsync(otp);
            await _otpRepository.SaveChangesAsync();

            return code;
        }

        public async Task<OtpVerificationResult> ValidateOtpAsync(int userId, OtpPurpose purpose, string code)
        {
            var otp = await _otpRepository.GetActiveOtpAsync(userId, purpose);

            if (otp is null || otp.IsUsed)
                return OtpVerificationResult.NotFound;

            if (otp.ExpiresAt < DateTime.UtcNow)
                return OtpVerificationResult.Expired;

            if (otp.Attempts >= MaxAttempts)
                return OtpVerificationResult.TooManyAttempts;

            var isMatch = otp.CodeHash == HashCode(code);

            if (!isMatch)
            {
                otp.Attempts++;
                await _otpRepository.SaveChangesAsync();
                return OtpVerificationResult.InvalidCode;
            }

            otp.IsUsed = true;
            await _otpRepository.SaveChangesAsync();

            return OtpVerificationResult.Success;
        }

        private static string GenerateSecureCode()
        {
            var bytes = RandomNumberGenerator.GetBytes(4);
            var value = BitConverter.ToUInt32(bytes, 0) % 1_000_000;
            return value.ToString("D6");
        }

        private static string HashCode(string code)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
            return Convert.ToHexString(bytes);
        }

        public async Task<OtpVerification?> GetActiveOtpAsync(int userId, OtpPurpose purpose) => await _otpRepository.GetActiveOtpAsync(userId, purpose);
    }
}