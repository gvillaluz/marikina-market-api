using MarikinaMarket.API.Application.DTOs.Auth.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.DTOs.Auth.Request;
using MarikinaMarket.API.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace MarikinaMarket.API.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _repository;
        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly IOtpService _otpService;
        private const int RENEW_AFTER_DAYS = 7;
        private const int REQUIRE_LOGIN_AFTER_DAYS = 60;

        public AuthService(
            IUserRepository repository,
            ITokenService tokenService,
            IUnitOfWork unitOfWork,
            IEmailService emailService,
            IOtpService otpService)
        {
            _repository = repository;
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _otpService = otpService;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var user = await ValidateCredentialsAsync(request.Username, request.Password);

            if (user is null)
                throw new InvalidCredentialsException("Invalid email or password");

            var userRole = await _repository.GetRoleAsync(user);

            if (userRole == Role.Enforcer)
                throw new UnauthorizedAccessException("This account is not allowed to use the web application.");

            var generatedAccessToken = await _tokenService.GenerateAccessToken(user);

            return new LoginResponse
            {
                AccessToken = generatedAccessToken,
                MustChangePassword = user.MustChangePassword
            };
        }

        public async Task<LoginMobileResponse> LoginMobileAsync(LoginRequest request)
        {
            var user = await ValidateCredentialsAsync(request.Username, request.Password);

            if (user is null)
                throw new InvalidCredentialsException("Invalid username or password");

            var generatedAccessToken = await _tokenService.GenerateAccessToken(user);
            var refreshToken = await _repository.AddRefreshTokenAsync(
                _tokenService.GenerateRefreshToken(user.Id)
            );

            var userRole = await _repository.GetRoleAsync(user);

            if (userRole != Role.Enforcer)
                throw new UnauthorizedAccessException("This account is not allowed to use the mobile application.");

            await _repository.SaveChangesAsync();

            return new LoginMobileResponse
            {
                AccessToken = generatedAccessToken,
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiration = refreshToken.ExpiresAt
            };
        }

        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            var existingUser = await _repository.FindByEmailAsync(request.EmailAddress);

            if (existingUser is not null)
                throw new ValidationException("Email is already registered.");

            var userNameSequence = await _repository.GetNextUserNameAsync();

            var userName = $"{request.Role.ToString()[..3].ToUpper()}{userNameSequence}-{DateTime.UtcNow.Year}";

            var user = new User
            {
                UserName = userName,
                FirstName = request.FirstName,
                MiddleName = request.MiddleName ?? null,
                Email = request.EmailAddress,
                LastName = request.LastName,
                Status = AccountStatus.Active,
                MustChangePassword = true,
                DateOfBirth = request.DateOfBirth,
                HouseNumber = request.HouseNumber,
                Street = request.Street,
                Barangay = request.Barangay,
                City = request.City
            };

            string defaultPassword = userName;

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                var userResponse = await _repository.CreateUserAsync(user, defaultPassword);

                if (!userResponse.Succeeded)
                {
                    await _unitOfWork.RollbackAsync();
                    var errors = string.Join("; ", userResponse.Errors.Select(e => e.Description));
                    throw new ValidationException($"Failed to register new account: {errors}");
                }

                var roleResponse = await _repository.AddToRoleAsync(user, request.Role.ToString());

                if (!roleResponse.Succeeded)
                {
                    await _unitOfWork.RollbackAsync();
                    var errors = string.Join("; ", roleResponse.Errors.Select(e => e.Description));
                    throw new ValidationException($"Failed to assign role: {errors}");
                }

                await _unitOfWork.CommitAsync();

                return new RegisterResponse
                {
                    UserId = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    Message = "User registered successfully."
                };
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<User?> ValidateCredentialsAsync(string userName, string password)
        {
            var user = await _repository.FindByUserNameAsync(userName);

            if (user is null)
                return null;

            var isPasswordValid = await _repository.CheckPasswordAsync(user, password);

            if (isPasswordValid.IsLockedOut)
                throw new AccountLockedException("Too many attempts. Please try again later.");

            if (!isPasswordValid.Succeeded)
                throw new InvalidCredentialsException("Invalid username or password.");

            return user;
        }

        public async Task<TokenRefreshResponse> RefreshTokensAsync(TokenRefreshRequest request)
        {
            var storedRefreshToken = await _repository.GetRefreshTokenAsync(request.RefreshToken);

            if (storedRefreshToken is null || storedRefreshToken.IsRevoked)
                throw new SessionExpiredException("Invalid refresh token. Please log in again.");

            if (storedRefreshToken.IsExpired)
                throw new SessionExpiredException("Session expired. Please log in again.");

            var tokenAge = DateTime.UtcNow - storedRefreshToken.CreatedAt;

            if (storedRefreshToken.User is null)
                throw new SessionExpiredException("Invalid refresh token. Please log in again.");

            var newAccessToken = await _tokenService.GenerateAccessToken(storedRefreshToken.User);

            if (tokenAge.TotalDays < RENEW_AFTER_DAYS)
            {
                return new TokenRefreshResponse
                {
                    AccessToken = newAccessToken,
                    RefreshToken = storedRefreshToken.Token,
                    RefreshTokenExpiration = storedRefreshToken.ExpiresAt
                };
            }

            storedRefreshToken.IsRevoked = true;

            var newRefreshToken = await _repository.AddRefreshTokenAsync(_tokenService.GenerateRefreshToken(storedRefreshToken.UserId));
            await _repository.SaveChangesAsync();

            if (newRefreshToken is null)
                throw new SessionExpiredException("Invalid refresh token. Please log in again.");

            return new TokenRefreshResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken.Token,
                RefreshTokenExpiration = newRefreshToken.ExpiresAt
            };
        }

        public Task<IdentityResult> MandatoryChangePasswordAsync(ChangePasswordRequest request, int userId)
            => ChangePasswordAsync(request, userId, clearMandatoryFlag: true);

        public async Task<IdentityResult> ChangePasswordAsync(ChangePasswordRequest request, int userId, bool clearMandatoryFlag = false)
        {
            var user = await _repository.GetUserAsync(userId);

            if (user is null)
                throw new RecordNotFoundException("User not found.");

            var result = await _repository.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

            if (result.Succeeded && clearMandatoryFlag)
            {
                user.MustChangePassword = false;
                await _repository.UpdateUserAsync(user);
            }

            return result;
        }

        public async Task<FindAccountResponse> FindAccountAsync(FindAccountRequest request)
        {
            var user = await _repository.FindByUserNameAsync(request.Username);

            if (user is null)
                throw new RecordNotFoundException("User not found.");

            return new FindAccountResponse
            {
                Found = user is not null,
                MaskedEmail = MaskEmail(user!.Email!) ?? "",
                MaskedPhoneNumber = MaskPhoneNumber(user.PhoneNumber!) ?? ""
            };
        }

        public async Task<SendCodeResponse> SendCodeAsync(SendCodeRequest request)
        {
            const int resendCooldownSeconds = 30;
            const int codeExpirySeconds = 90;

            var user = await _repository.FindByUserNameAsync(request.Username);

            if (user is null)
                return new SendCodeResponse
                {
                    Message = "OTP Sent.",
                    ResendCooldownSeconds = resendCooldownSeconds,
                    CodeExpirySeconds = codeExpirySeconds
                };

            var latestOtp = await _otpService.GetActiveOtpAsync(user.Id, OtpPurpose.ResetPassword);

            if (latestOtp is not null)
            {
                var secondsSinceSent = (DateTime.UtcNow - latestOtp.CreatedAt).TotalSeconds;
                if (secondsSinceSent < resendCooldownSeconds)
                {
                    return new SendCodeResponse
                    {
                        Message = "OTP Sent.",
                        ResendCooldownSeconds = resendCooldownSeconds - (int)secondsSinceSent,
                        CodeExpirySeconds = codeExpirySeconds
                    };
                }
            }

            var otp = await _otpService.GenerateOtpAsync(user.Id, OtpPurpose.ResetPassword);

            if (request.Channel.Equals("email"))
            {
                var expiryMinutes = codeExpirySeconds / 60.0;

                var emailBody = $@"
                <div style=""font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 24px; color: #333;"">
                    <h2 style=""color: #0F3D7A; margin-bottom: 4px;"">Marikina Public Market Inspection System</h2>
                    <p style=""color: #666; margin-top: 0;"">Password Reset Request</p>

                    <p>Hello,</p>
                    <p>We received a request to reset the password for your account. Use the verification code below to continue:</p>

                    <div style=""background: #F5F5F5; border-radius: 8px; padding: 16px; text-align: center; margin: 24px 0;"">
                        <span style=""font-size: 28px; font-weight: bold; letter-spacing: 6px; color: #0F3D7A;"">{otp}</span>
                    </div>

                    <p>This code will expire in <strong>{expiryMinutes:0.#} minute(s)</strong>.</p>
                    <p>If you didn't request a password reset, you can safely ignore this email — your password will remain unchanged.</p>

                    <p style=""color: #999; font-size: 12px; margin-top: 32px;"">This is an automated message, please do not reply.</p>
                </div>";

                await _emailService.SendEmailAsync(
                    user.Email!,
                    "Reset Password OTP",
                    emailBody
                );
            }
            else
            {
                // TODO: ADD SMS SERVICE
            }

            return new SendCodeResponse
            {
                Message = "OTP Sent.",
                ResendCooldownSeconds = resendCooldownSeconds,
                CodeExpirySeconds = codeExpirySeconds
            };
        }

        public async Task<VerifyCodeResponse> VerifyCodeAsync(VerifyCodeRequest request)
        {
            var user = await _repository.FindByUserNameAsync(request.Username);

            if (user is null)
                return new VerifyCodeResponse { Success = false, ResetToken = null, Message = "Invalid or expired code." };

            var result = await _otpService.ValidateOtpAsync(user.Id, OtpPurpose.ResetPassword, request.Code);

            return result switch
            {
                OtpVerificationResult.Success => new VerifyCodeResponse
                {
                    Success = true,
                    ResetToken = await _repository.GenerateResetPassTokenAsync(user),
                    Message = "Code verified.",
                },
                OtpVerificationResult.TooManyAttempts => new VerifyCodeResponse
                {
                    Success = false,
                    ResetToken = null,
                    Message = "Too many incorrect attempts. Please request a new code.",
                },
                OtpVerificationResult.Expired => new VerifyCodeResponse
                {
                    Success = false,
                    ResetToken = null,
                    Message = "This code has expired. Please request a new one.",
                },
                _ => new VerifyCodeResponse { Success = false, ResetToken = null, Message = "Invalid or expired code." },
            };
        }

        public async Task<ResetPasswordResponse> ResetPasswordAsync(ResetPasswordRequest request)
        {
            var user = await _repository.FindByUserNameAsync(request.Username);

            if (user is null)
                return new ResetPasswordResponse
                {
                    Success = false,
                    Message = "User not found.",
                };

            var result = await _repository.ResetPasswordByUsernameAsync(user, request.ResetToken, request.NewPassword);

            if (!result.Succeeded) 
                return new ResetPasswordResponse
                {
                    Success = false,
                    Message = "Failed to reset password."
                };

            return new ResetPasswordResponse
            {
                Success = true,
                Message = "Password reset successfully."
            };
        }

        private string? MaskEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return null;

            var parts = email.Split('@');
            var name = parts[0];
            var domain = parts[1];

            var visibleCount = Math.Min(3, Math.Max(name.Length - 1, 1));
            string maskedName = name.Length <= visibleCount
                ? name
                : name[..visibleCount] + new string('*', name.Length - visibleCount);

            return $"{maskedName}@{domain}";
        }

        private string? MaskPhoneNumber(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone) || phone.Length < 7) return null;

            var prefixLength = phone.StartsWith('+') ? 4 : 3;
            var prefix = phone[..prefixLength];
            var lastFour = phone[^4..];

            return $"{prefix}****{lastFour}";
        }
    }
}