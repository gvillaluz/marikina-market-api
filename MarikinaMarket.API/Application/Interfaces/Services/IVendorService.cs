using MarikinaMarket.API.Application.DTOs.User.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Application.DTOs.Vendor.Request;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IVendorService
    {
        public Task<RegisterVendorResponse> CreateVendorRegistryAsync(RegisterVendorRequest request);
        public Task<RegistrationApprovalResponse> CreateAdminVendorRegistrationAsync(
            AdminRegisterVendorRequest request,
            int adminId);
        public Task<RegistrationApprovalResponse> ApproveVendorRegistration(
            int registrationId,
            RegistrationAdminAction request);
        public Task<RegistrationDeclinedResponse> DeclineVendorRegistration(
            int registrationId,
            RegistrationAdminAction request);
        public Task<RegistrationDeclinedResponse> RequestMoreInformation(
            int registrationId,
            RegistrationAdminAction request);
        public Task<VendorRegistrationStatusCountsResponse> GetVendorRegistrationStatusCountsAsync();
        public Task<PageResponse<VendorRegistrationSummaryResponse>> GetVendorRegistrationSummariesAsync(
            int offset,
            VendorRegistrationRequestFilters filters);
        public Task<VendorRegistrationDetailsResponse> GetVendorRegistrationDetailsAsync(int registrationId);
        public Task<List<VendorRegistrationDocumentResponse>> GetVendorRegistrationDocumentsAsync(int registrationId);
        public Task<List<GetVendorResponse>> GetVendorByBusinessIdAsync(string stallNumber);
        public Task<GetVendorResponse> GetVendorByQrCode(string qrCode);
        public Task<PageResponse<AdminVendorSummaryResponse>> GetAdminVendorSummariesAsync(
            int offset,
            AdminVendorSummaryFilter filters);
        public Task<AdminVendorComplianceOverviewResponse> GetAdminVendorComplianceOverviewAsync();
        public Task<PageResponse<AdminPendingTicketSettlementResponse>> GetPendingTicketSettlementsAsync(int offset);
        public Task<VendorProfileResponse> GetVendorProfileAsync(int vendorId);
        public Task<VendorComplianceScoreResponse> GetVendorComplianceScoreAsync(int vendorId);
    }
}
