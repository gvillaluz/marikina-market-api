using MarikinaMarket.API.Application.DTOs.Enforcers.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;
using MarikinaMarket.API.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IUserService
    {
        Task<UserProfileResponse> GetUserInfoAsync(int userId);
        Task<UserProfileResponse> UpdateUserInfo(EditUserRequest request, int userId);
        Task RegisterDeviceTokenAsync(int userId, string deviceToken);
        Task<UserProfileResponse> ChangeProfilePhoto(int userId, IFormFile file);
        Task<UserProfileResponse> RemoveProfilePhoto(int userId);
    }
}
