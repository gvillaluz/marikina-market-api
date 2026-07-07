using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IVendorService
    {
        public Task<RegisterVendorResponse> CreateVendorRegistryAsync(RegisterVendorRequest request);
    }
}
