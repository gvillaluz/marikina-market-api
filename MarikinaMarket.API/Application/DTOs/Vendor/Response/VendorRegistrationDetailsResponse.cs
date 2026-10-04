using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class VendorRegistrationDetailsResponse
    {
        public int RegistrationId { get; set; }
        public required string BusinessId { get; set; }
        public required string BusinessName { get; set; }
        public required string NatureOfBusiness { get; set; }
        public VendorType VendorType { get; set; }
        public required string MarketSectionName { get; set; }
        public string? StallNumber { get; set; }
        public required string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public required string LastName { get; set; }
        public DateOnly DateOfBirth { get; set; }
        public int Age { get; set; }
        public GovernmentIdType GovernmentIdType { get; set; }
        public required string GovernmentIdNumber { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Email { get; set; }
        public required string HouseNumber { get; set; }
        public required string Street { get; set; }
        public required string Barangay { get; set; }
        public required string City { get; set; }
        public RequestStatus Status { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewerName { get; set; }
        public string? RemarksOrReason { get; set; }
        public string? ReviewReason { get; set; }
        public string? ReviewRemarks { get; set; }
        public required List<VendorRegistrationDocumentResponse> Documents { get; set; }
    }
}
