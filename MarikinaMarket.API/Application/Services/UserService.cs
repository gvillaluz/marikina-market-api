using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;

        private const int RENEW_AFTER_DAYS = 7;
        private const int REQUIRE_LOGIN_AFTER_DAYS = 60;

        public UserService(IUserRepository userRepository, ITokenService tokenService)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var user = await ValidateCredentialsAsync(request.UserName, request.Password);

            if (user is null)
                throw new Exception("Invalid email or password");

            var userRole = await _userRepository.GetRoleAsync(user);

            if (userRole == Role.Enforcer)
                throw new UnauthorizedAccessException("This account is not allowed to use the mobile application.");

            var generatedAccessToken = await _tokenService.GenerateAccessToken(user);

            return new LoginResponse { AccessToken = generatedAccessToken };
        }

        public async Task<LoginMobileResponse> LoginMobileAsync(LoginRequest request)
        {
            var user = await ValidateCredentialsAsync(request.UserName, request.Password);

            if (user is null)
                throw new Exception("Invalid email or password");

            var generatedAccessToken = await _tokenService.GenerateAccessToken(user);
            var refreshToken = await _userRepository.AddRefreshTokenAsync(
                    _tokenService.GenerateRefreshToken(user.Id)
                );

            var userRole = await _userRepository.GetRoleAsync(user);

            if (userRole != Role.Enforcer)
                throw new UnauthorizedAccessException("This account is not allowed to use the mobile application.");

            return new LoginMobileResponse
            {
                AccessToken = generatedAccessToken,
                RefreshToken = refreshToken.Token
            };
        }

        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            var existingUser = await _userRepository.FindByEmailAsync(request.EmailAddress);

            if (existingUser is not null)
                throw new Exception("Email is already registered.");

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

            var userResponse = await _userRepository.CreateUserAsync(user, defaultPassword);

            if (!userResponse.Succeeded)
                throw new Exception("Failed to register new account. Please try again later.");

            await _userRepository.AddToRoleAsync(user, request.Role.ToString());

            return new RegisterResponse
            {
                UserId = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                Message = "User registered successfully."
            };
        }

        public async Task<User?> ValidateCredentialsAsync(string userName, string password)
        {
            var user = await _userRepository.FindByUserNameAsync(userName);

            if (user is null)
                throw new Exception("Invalid email or password");

            var isPasswordValid = await _userRepository.CheckPasswordAsync(user, password);

            if (isPasswordValid.IsLockedOut)
                throw new Exception("Too many attempts. Please try again later.");

            if (!isPasswordValid.Succeeded)
                throw new Exception("Invalid email or password.");

            return user;
        }

        public async Task<TokenRefreshResponse> RefreshTokens(TokenRefreshRequest request)
        {
            var storedRefreshToken = await _userRepository.GetRefreshTokenAsync(request.RefreshToken);

            if (storedRefreshToken is null || storedRefreshToken.IsRevoked)
                throw new Exception("Invalid refresh token. Please log in again.");

            if (storedRefreshToken.IsExpired)
                throw new Exception("Session expired. Please log in again.");

            var tokenAge = DateTime.UtcNow - storedRefreshToken.CreatedAt;

            if (storedRefreshToken.User is null)
                throw new Exception("Invalid refresh token. Please log in again.");

            var newAccessToken = await _tokenService.GenerateAccessToken(storedRefreshToken.User);

            if (tokenAge.TotalDays < 7)
            {
                return new TokenRefreshResponse
                {
                    NewAccessToken = newAccessToken,
                    RefreshToken = storedRefreshToken.Token,
                };
            }

            var newRefreshToken = _tokenService.GenerateRefreshToken(storedRefreshToken.UserId);
            await _userRepository.SaveChangesAsync();

            if (newRefreshToken is null)
                throw new Exception("Invalid refresh token. Please log in again.");

            return new TokenRefreshResponse
            {
                NewAccessToken = newAccessToken,
                RefreshToken = newRefreshToken.Token
            };
        }
    }
}
