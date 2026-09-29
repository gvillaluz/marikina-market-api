using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using System.Data;

namespace MarikinaMarket.API.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IStorageService _storageService;

        public UserService(
            IUserRepository userRepository, 
            IStorageService storageService)
        {
            _userRepository = userRepository;
            _storageService = storageService;
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
                DateOfBirth = user.DateOfBirth,
                MobileNumber = user.PhoneNumber ?? "",
                HouseNumber = user.HouseNumber,
                Street = user.Street,
                Barangay = user.Barangay,
                City = user.City,
                Status = user.Status,
                Role = role,
                ProfileUrl = user.ProfilePictureUrl,
                CreatedAt = user.CreatedAt,
                MustChangedPassword = user.MustChangePassword
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
                DateOfBirth = user.DateOfBirth,
                MobileNumber = user.PhoneNumber ?? "",
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
                MobileNumber = user.PhoneNumber ?? "",
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
                MobileNumber = user.PhoneNumber ?? "",
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
