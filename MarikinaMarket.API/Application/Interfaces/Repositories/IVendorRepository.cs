using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface IVendorRepository
    {
        public Task<VendorTicketSummary?> GetByIdAsync(int id);
        public Task<VendorProfile> CreateVendorAsync(VendorProfile vendorProfile);
        public Task<VendorRegistrationRequest> AddVendorRegistryAsync(VendorRegistrationRequest vendor);
        public Task<VendorRegistrationRequest?> GetRegistrationById(int registrationId);
        public Task<List<VendorLookupResult>> GetVendorByStallNumber(string stallNumber);
        public Task<VendorLookupResult?> GetVendorByQrCode(string codeValue);
        public Task SaveChangesAsync();
    }
}
