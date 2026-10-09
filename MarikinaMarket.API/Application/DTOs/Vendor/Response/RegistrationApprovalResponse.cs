using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class RegistrationApprovalResponse
    {
        public required int RegistrationId { get; set; }
        public required int UserId { get; set; }
        public required int VendorId { get; set; }
        public required string FirstName { get; set; }
        public required string MiddleName {  get; set; }
        public required string LastName { get; set; }
        public required string BusinessName {  get; set; }
        public required int MarketSectionId { get; set; }
        public required string MarketSectionName { get; set; }
        public required int AdminId { get; set; }
        public required RequestStatus Status { get; set; }
        public required VendorStatus VendorStatus { get; set; }
        public required DateTime ReviewedAt { get; set; }
        public bool EmailSent { get; set; }
        public required DateTime CreatedAt { get; set; }
        public required uint Version { get; set; }
    }
}
