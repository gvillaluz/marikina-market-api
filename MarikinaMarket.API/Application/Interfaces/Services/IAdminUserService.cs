using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.User.Response;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IAdminUserService
    {
        Task<AdminUserResponse> CreateUserAsync(CreateAdminUserRequest request, int adminId);
    }
}
