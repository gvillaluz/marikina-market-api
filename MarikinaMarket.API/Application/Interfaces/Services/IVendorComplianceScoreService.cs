using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IVendorComplianceScoreService
    {
        VendorComplianceScoreResponse Calculate(
            IReadOnlyCollection<VendorComplianceTicket> tickets,
            DateTime calculatedAt);
    }
}
