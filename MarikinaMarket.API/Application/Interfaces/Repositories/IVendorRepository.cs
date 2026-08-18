using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface IVendorRepository
    {
        public Task<VendorTicketSummary?> GetByIdAsync(int id);
        public Task<WarningCheck> CheckHasWarning(int vendorId);
        public Task<List<WarningCheck>> CheckHasWarningList(List<int> vendorIds);
        public Task<VendorProfile> CreateVendorAsync(VendorProfile vendorProfile);
        public Task<VendorRegistrationRequest> AddVendorRegistryAsync(VendorRegistrationRequest vendor);
        public Task<VendorRegistrationRequest?> GetRegistrationById(int registrationId);
        public Task<List<VendorLookupResult>> GetVendorByStallNumber(string stallNumber);
        public Task<VendorLookupResult?> GetVendorByQrCode(string codeValue);
        public void SetOriginalVersion(VendorRegistrationRequest request, uint version);
        public Task SaveChangesAsync();
    }
}
