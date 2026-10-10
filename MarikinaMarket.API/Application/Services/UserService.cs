using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace MarikinaMarket.API.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IStorageService _storageService;
        private readonly IUnitOfWork? _unitOfWork;
        private readonly IAuditLogService? _auditService;
        private const int PAGE_SIZE = 10;

        public UserService(
            IUserRepository userRepository, 
            IStorageService storageService, IUnitOfWork? unitOfWork = null, IAuditLogService? auditService = null)
        {
            _userRepository = userRepository;
            _storageService = storageService;
            _unitOfWork = unitOfWork;
            _auditService = auditService;
        }

        public Task<AccountCountsResponse> GetAccountCountsAsync()
            => _userRepository.GetAccountCountsAsync();

        public async Task<PageResponse<UserSummaryResponse>> GetUserSummariesAsync(int offset, UserSummaryFilter filters)
        {
            if (offset < 0)
                throw new ValidationException("Offset must be zero or greater.");

            Validator.ValidateProperty(filters.Role, new ValidationContext(filters)
            {
                MemberName = nameof(UserSummaryFilter.Role)
            });

            if (filters.Search?.Length > 200)
                throw new ValidationException("Search term must be 200 characters or fewer.");

            var users = await _userRepository.GetUserSummariesAsync(offset, PAGE_SIZE, filters);
            var total = await _userRepository.GetUserSummariesCountAsync(filters);
            var hasMore = users.Count > PAGE_SIZE;
            if (hasMore)
                users.RemoveAt(users.Count - 1);

            var profileKeys = users.Select(u => u.ProfileUrl)
                .OfType<string>()
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Distinct()
                .ToList();

            var profiles = profileKeys.Count > 0
                ? await _storageService.GetPresignedUrlsAsync(B2BucketType.General, profileKeys)
                : new Dictionary<string, string>();

            foreach (var user in users)
            {
                string? profileUrl = null;
                if (!string.IsNullOrWhiteSpace(user.ProfileUrl))
                    profiles.TryGetValue(user.ProfileUrl, out profileUrl);

                user.ProfileUrl = profileUrl;
            }

            return new PageResponse<UserSummaryResponse>
            {
                Items = users,
                HasMore = hasMore,
                Total = total
            };
        }

        public async Task<UserProfileResponse> GetUserInfoAsync(int userId)
        {
            var user = await _userRepository.GetUserAsync(userId);

            if (user is null)
            {
                throw new RecordNotFoundException("User not found.");
            }

            return await BuildUserProfileResponseAsync(user);
        }

        private async Task<UserProfileResponse> BuildUserProfileResponseAsync(User user)
        {
            var role = await _userRepository.GetRoleAsync(user);
            var profileUrl = string.IsNullOrWhiteSpace(user.ProfilePictureUrl)
                ? null
                : await _storageService.GetPresignedUrlAsync(B2BucketType.General, user.ProfilePictureUrl);

            return new UserProfileResponse
            {
                UserId = user.Id,
                Username = user.UserName ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                MiddleName = user.MiddleName,
                Email = user.Email ?? "",
                DateOfBirth = user.DateOfBirth,
                PhoneNumber = user.PhoneNumber ?? "",
                HouseNumber = user.HouseNumber,
                Street = user.Street,
                Barangay = user.Barangay,
                City = user.City,
                Status = user.Status,
                Role = role,
                ProfileUrl = profileUrl,
                CreatedAt = user.CreatedAt,
                MustChangedPassword = user.MustChangePassword
            };
        }

        public async Task<UserProfileResponse> UpdateUserInfo(EditUserRequest request, int userId)
        {
            Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);
            if (userId <= 0)
                throw new ValidationException("User ID must be greater than zero.");

            var dateOfBirth = request.DateOfBirth ?? throw new ValidationException("Birth date is required.");
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Asia/Manila"));
            if (dateOfBirth == DateOnly.MinValue || dateOfBirth > today)
                throw new ValidationException("Birth date must be a valid date that is not in the future.");

            var user = await _userRepository.GetUserAsync(userId);

            if (user is null)
            {
                throw new RecordNotFoundException("User not found.");
            }

            var email = request.Email.Trim();
            var existingEmail = await _userRepository.FindByEmailAsync(email);
            if (existingEmail is not null && existingEmail.Id != userId)
                throw new ValidationException("Email is already in use.");

            if (_unitOfWork is not null) await _unitOfWork.BeginTransactionAsync();
            try
            {
                if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
                    user.EmailConfirmed = false;
                if (!string.Equals(user.PhoneNumber, request.PhoneNumber, StringComparison.Ordinal))
                    user.PhoneNumberConfirmed = false;

                user.LastName = request.LastName.Trim();
                user.FirstName = request.FirstName.Trim();
                user.MiddleName = string.IsNullOrWhiteSpace(request.MiddleName) ? null : request.MiddleName.Trim();
                user.DateOfBirth = dateOfBirth;
                user.Email = email;
                user.PhoneNumber = request.PhoneNumber;
                user.HouseNumber = request.HouseNumber.Trim();
                user.Street = request.Street.Trim();
                user.Barangay = request.Barangay.Trim();
                user.City = request.City.Trim();

                var updateResult = await _userRepository.UpdateUserAsync(user);

                if (!updateResult.Succeeded)
                {
                    var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                    throw new ValidationException(errors);
                }

                if (_auditService is not null) await _auditService.StageCurrentAsync();
                if (_unitOfWork is not null) await _unitOfWork.CommitAsync();
            }
            catch
            {
                if (_unitOfWork is not null) await _unitOfWork.RollbackAsync();
                throw;
            }

            return await BuildUserProfileResponseAsync(user);
        }

        public async Task RegisterDeviceTokenAsync(int userId, string deviceToken)
        {
            var existingToken = await _userRepository.GetDeviceTokenByValueAsync(deviceToken);

            if (existingToken is not null) 
            {
                if (existingToken.UserId == userId)
                {
                    existingToken.LastUsedAt = DateTime.UtcNow;
                    await _userRepository.SaveChangesAsync();
                    return;
                }

                existingToken.UserId = userId;
                existingToken.LastUsedAt = DateTime.UtcNow;
            }
            else
            {
                var userDeviceToken = new UserDeviceToken
                {
                    UserId = userId,
                    DeviceToken = deviceToken,
                    CreatedAt = DateTime.UtcNow,
                    LastUsedAt = DateTime.UtcNow
                };
                await _userRepository.AddDeviceTokenAsync(userDeviceToken);
            }
            await _userRepository.SaveChangesAsync();
        }

        public async Task<UserProfileResponse> ChangeProfilePhoto(int userId, IFormFile file)
        {
            var user = await _userRepository.GetUserAsync(userId);

            if (user is null)
                throw new RecordNotFoundException("User not found.");

            if (user.ProfilePictureUrl != null && string.IsNullOrEmpty(user.ProfilePictureUrl))
            {
                await _storageService.DeleteFileAsync(B2BucketType.General, user.ProfilePictureUrl);
            }

            var generatedFileKey = GenerateFileKey(file);

            var fileKey = await _storageService.UploadFileAsync(B2BucketType.General, file, generatedFileKey);

            user.ProfilePictureUrl = fileKey;

            var updateResult = await _userRepository.UpdateUserAsync(user);

            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                throw new ValidationException(errors);
            }

            var presignedUrl = await _storageService.GetPresignedUrlAsync(B2BucketType.General, fileKey);

            var role = await _userRepository.GetRoleAsync(user);

            return new UserProfileResponse
            {
                UserId = user.Id,
                Username = user.UserName ?? "",
                FirstName = user.FirstName,
                LastName = user.LastName,
                MiddleName = user.MiddleName,
                Email = user.Email ?? "",
                DateOfBirth = user.DateOfBirth,
                PhoneNumber = user.PhoneNumber ?? "",
                HouseNumber = user.HouseNumber,
                Street = user.Street,
                Barangay = user.Barangay,
                City = user.City,
                Status = user.Status,
                Role = role,
                CreatedAt = user.CreatedAt,
                ProfileUrl = presignedUrl,
                MustChangedPassword = user.MustChangePassword
            };
        }

        public async Task<UserProfileResponse> RemoveProfilePhoto(int userId)
        {
            var user = await _userRepository.GetUserAsync(userId);

            if (user is null)
                throw new RecordNotFoundException("User not found.");

            if (user.ProfilePictureUrl is not null)
            {
                await _storageService.DeleteFileAsync(B2BucketType.General, user.ProfilePictureUrl);   
            }

            user.ProfilePictureUrl = null;

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
                DateOfBirth = user.DateOfBirth,
                PhoneNumber = user.PhoneNumber ?? "",
                HouseNumber = user.HouseNumber,
                Street = user.Street,
                Barangay = user.Barangay,
                City = user.City,
                Status = user.Status,
                Role = role,
                CreatedAt = user.CreatedAt,
                ProfileUrl = user.ProfilePictureUrl,
                MustChangedPassword = user.MustChangePassword
            };
        }

        private static string GenerateFileKey(IFormFile file)
        {
            string fileExtension = Path.GetExtension(file.FileName);
            string datePath = DateTime.UtcNow.ToString("yyyy/MM");
            return $"tickets/{datePath}/{Guid.NewGuid()}{fileExtension}";
        }
    }
}
