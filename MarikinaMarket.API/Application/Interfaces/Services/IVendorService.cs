using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IVendorService
    {
        public Task<RegisterVendorResponse> CreateVendorRegistryAsync(RegisterVendorRequest request);
        public Task<List<GetVendorResponse>> GetVendorByStallNumberAsync(string stallNumber);
        public Task<GetVendorResponse> GetVendorByQrCode(string qrCode);
    }
}
