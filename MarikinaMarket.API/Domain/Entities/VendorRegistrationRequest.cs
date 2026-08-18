using MarikinaMarket.API.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace MarikinaMarket.API.Domain.Entities
{
    public class VendorRegistrationRequest
    {
        public int Id { get; set; }
        public GovernmentIdType GovernmentIdType { get; set; }
        public required string GovernmentIdNumber { get; set; }
        public required string GovernmentIdPhotoUrl { get; set; }
        public required string BusinessDocumentPhotoUrl { get; set; }
        public required string BusinessName { get; set; }
        public required string NatureOfBusiness { get; set; }
        public required string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public required string LastName { get; set; }
        public DateOnly DateOfBirth { get; set; }
        public int Age { get; set; }
        public required string HouseNumber { get; set; }
        public required string Street { get; set; }
        public required string Barangay { get; set; }
        public required string City { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string StallNumber { get; set; }

        public required int MarketSectionId { get; set; }
        public MarketSection? MarketSection { get; set; }

        public RequestStatus Status { get; set; }
        public DateTime RequestedAt {  get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }

        public int? ReviewedBy { get; set; }
        public User? ReviewedByUser { get; set; }
        public string? RemarksOrReason { get; set; }
        public uint Version { get; set; }
    }
}
