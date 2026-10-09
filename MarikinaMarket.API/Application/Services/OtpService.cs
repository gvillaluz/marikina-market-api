using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Services
{
    public class OtpService : IOtpService
    {
        private readonly IOtpRepository _otpRepository;
        private readonly IUnitOfWork _unitOfWork;
        private const int MaxAttempts = 5;

        public OtpService(IOtpRepository otpRepository, IUnitOfWork unitOfWork)
        {
            _otpRepository = otpRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<string> GenerateOtpAsync(int userId, OtpPurpose purpose, int expiryMinutes = 10)
        {
            if (userId <= 0 || !Enum.IsDefined(purpose) || expiryMinutes <= 0)
                throw new ValidationException("Invalid OTP generation request.");

            var code = GenerateSecureCode();
            var now = DateTime.UtcNow;

            var otp = new OtpVerification
            {
                UserId = userId,
                CodeHash = HashCode(code),
                Purpose = purpose,
                ExpiresAt = now.AddMinutes(expiryMinutes),
                Attempts = 0,
                CreatedAt = now
            };

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                await _otpRepository.InvalidatePriorOtpsAsync(userId, purpose);
                await _otpRepository.AddOtpAsync(otp);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }

            return code;
        }

        public async Task<OtpVerificationResult> ValidateOtpAsync(int userId, OtpPurpose purpose, string code)
        {
            if (userId <= 0 || !Enum.IsDefined(purpose) ||
                code is null || !Regex.IsMatch(code, @"\A[0-9]{6}\z"))
                return OtpVerificationResult.InvalidCode;

            var otp = await _otpRepository.GetLatestOtpAsync(userId, purpose);
            var failure = GetValidationFailure(otp);
            if (failure.HasValue)
                return failure.Value;

            var isMatch = CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(otp!.CodeHash), Encoding.UTF8.GetBytes(HashCode(code)));

            if (!isMatch)
            {
                await _otpRepository.IncrementAttemptsAsync(otp.Id, MaxAttempts);
                var latest = await _otpRepository.GetLatestOtpAsync(userId, purpose);
                return GetValidationFailure(latest) ?? OtpVerificationResult.InvalidCode;
            }

            if (!await _otpRepository.TryUseOtpAsync(otp.Id, MaxAttempts))
            {
                var latest = await _otpRepository.GetLatestOtpAsync(userId, purpose);
                return GetValidationFailure(latest) ?? OtpVerificationResult.NotFound;
            }

            return OtpVerificationResult.Success;
        }

        private static string GenerateSecureCode()
        {
            var value = RandomNumberGenerator.GetInt32(1_000_000);
            return value.ToString("D6");
        }

        private static OtpVerificationResult? GetValidationFailure(OtpVerification? otp)
        {
            if (otp is null || otp.IsUsed)
                return OtpVerificationResult.NotFound;
            if (otp.ExpiresAt <= DateTime.UtcNow)
                return OtpVerificationResult.Expired;
            if (otp.Attempts >= MaxAttempts)
                return OtpVerificationResult.TooManyAttempts;

            return null;
        }

        private static string HashCode(string code)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
            return Convert.ToHexString(bytes);
        }

        public async Task<OtpVerification?> GetLatestOtpAsync(int userId, OtpPurpose purpose) => await _otpRepository.GetLatestOtpAsync(userId, purpose);
    }
}
