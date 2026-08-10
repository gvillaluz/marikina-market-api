using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace MarikinaMarket.API.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _unitOfWork;

        private const int RENEW_AFTER_DAYS = 7;
        private const int REQUIRE_LOGIN_AFTER_DAYS = 60;

        public UserService(
            IUserRepository userRepository, 
            ITokenService tokenService,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _unitOfWork = unitOfWork;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var user = await ValidateCredentialsAsync(request.Username, request.Password);

            if (user is null)
                throw new InvalidCredentialsException("Invalid email or password");

            var userRole = await _userRepository.GetRoleAsync(user);

            if (userRole == Role.Enforcer)
                throw new UnauthorizedAccessException("This account is not allowed to use the web application.");

            var generatedAccessToken = await _tokenService.GenerateAccessToken(user);

            return new LoginResponse 
            { 
                AccessToken = generatedAccessToken,
            };
        }

        public async Task<LoginMobileResponse> LoginMobileAsync(LoginRequest request)
        {
            var user = await ValidateCredentialsAsync(request.Username, request.Password);

            if (user is null)
                throw new InvalidCredentialsException("Invalid username or password");

            var generatedAccessToken = await _tokenService.GenerateAccessToken(user);
            var refreshToken = await _userRepository.AddRefreshTokenAsync(
                _tokenService.GenerateRefreshToken(user.Id)
            );

            var userRole = await _userRepository.GetRoleAsync(user);

            if (userRole != Role.Enforcer)
                throw new UnauthorizedAccessException("This account is not allowed to use the mobile application.");

            await _userRepository.SaveChangesAsync();

            return new LoginMobileResponse
            {
                AccessToken = generatedAccessToken,
                RefreshToken = refreshToken.Token,
                RefreshTokenExpiration = refreshToken.ExpiresAt
            };
        }

        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            var existingUser = await _userRepository.FindByEmailAsync(request.EmailAddress);

            if (existingUser is not null)
                throw new ValidationException("Email is already registered.");

            var userNameSequence = await _userRepository.GetNextUserNameAsync();

            var userName = $"{request.Role.ToString()[..3].ToUpper()}{userNameSequence}-{DateTime.UtcNow.Year}";

            var user = new User
            {
                UserName = userName,
                FirstName = request.FirstName,
                MiddleName = request.MiddleName ?? null,
                Email = request.EmailAddress,
                LastName = request.LastName,
                Status = AccountStatus.Active,
                MustChangedPassword = true
            };

            string defaultPassword = userName;

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                var userResponse = await _userRepository.CreateUserAsync(user, defaultPassword);

                if (!userResponse.Succeeded)
                {
                    await _unitOfWork.RollbackAsync();
                    var errors = string.Join("; ", userResponse.Errors.Select(e => e.Description));
                    throw new ValidationException($"Failed to register new account: {errors}");
                }

                var roleResponse = await _userRepository.AddToRoleAsync(user, request.Role.ToString());

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
            var user = await _userRepository.FindByUserNameAsync(userName);

            if (user is null)
                return null;

            var isPasswordValid = await _userRepository.CheckPasswordAsync(user, password);

            if (isPasswordValid.IsLockedOut)
                throw new AccountLockedException("Too many attempts. Please try again later.");

            if (!isPasswordValid.Succeeded)
                throw new InvalidCredentialsException("Invalid email or password.");

            return user;
        }

        public async Task<TokenRefreshResponse> RefreshTokensAsync(TokenRefreshRequest request)
        {
            var storedRefreshToken = await _userRepository.GetRefreshTokenAsync(request.RefreshToken);

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

            var newRefreshToken = await _userRepository.AddRefreshTokenAsync(_tokenService.GenerateRefreshToken(storedRefreshToken.UserId));
            await _userRepository.SaveChangesAsync();

            if (newRefreshToken is null)
                throw new SessionExpiredException("Invalid refresh token. Please log in again.");

            return new TokenRefreshResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken.Token,
                RefreshTokenExpiration = newRefreshToken.ExpiresAt
            };
        }

        public async Task<UserProfileResponse> GetUserInfoAsync(int userId)
        {
            var user = await _userRepository.GetUserAsync(userId);

            if (user is null)
            {
                throw new RecordNotFoundException("User not found.");
            }

            var role = await _userRepository.GetRoleAsync(user);

            return new UserProfileResponse
            {
                UserId = user.Id,
                Username = user.UserName ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                MiddleName = user.MiddleName,
                Email = user.Email ?? "",
                Status = user.Status,
                Role = role,
                CreatedAt = user.CreatedAt,
                MustChangedPassword = user.MustChangedPassword
            };
        }

        public async Task<UserProfileResponse> UpdateUserInfo(EditUserRequest request, int userId)
        {
            var user = await _userRepository.GetUserAsync(userId);

            if (user is null)
            {
                throw new RecordNotFoundException("User not found.");
            }

            user.LastName = request.LastName;
            user.FirstName = request.FirstName;
            user.MiddleName = request.MiddleName;

            var updateResult = await _userRepository.UpdateUserAsync(user);

            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                throw new ValidationException(errors);
            }

            var role = await _userRepository.GetRoleAsync(user);

            return new UserProfileResponse
            {
                UserId = user.Id,
                Username = user.UserName ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                MiddleName = user.MiddleName,
                Email = user.Email ?? "",
                Status = user.Status,
                Role = role,
                CreatedAt = user.CreatedAt,
                MustChangedPassword = user.MustChangedPassword
            };
        }

        public Task<IdentityResult> MandatoryChangePasswordAsync(ChangePasswordRequest request, int userId)
            => ChangePasswordAsync(request, userId, clearMandatoryFlag: true);

        public async Task<IdentityResult> ChangePasswordAsync(ChangePasswordRequest request, int userId, bool clearMandatoryFlag = false)
        {
            var user = await _userRepository.GetUserAsync(userId);

            if (user is null)
                throw new RecordNotFoundException("User not found.");

            var result = await _userRepository.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

            if (result.Succeeded && clearMandatoryFlag)
            {
                user.MustChangedPassword = false;
                await _userRepository.UpdateUserAsync(user);
            }

            return result;
        }
    }
}
