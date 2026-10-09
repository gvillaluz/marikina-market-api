using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IVendorComplianceScoreService
    {
        // The caller owns the transaction so ticket and score writes commit together.
        Task<VendorComplianceScoreResponse> RefreshAsync(int vendorId, DateTime calculatedAt);
        Task RefreshAllAsync(DateTime calculatedAt);
        VendorComplianceScoreResponse Calculate(
            IReadOnlyCollection<VendorComplianceTicket> tickets,
            DateTime calculatedAt);
    }
}
