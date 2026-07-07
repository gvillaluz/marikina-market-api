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
        public required string FirstName { get; set; }
        public string? MiddleName { get; set; }
        public required string LastName { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
        public RequestStatus Status { get; set; }
        public DateTime RequestedAt {  get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }

        public int? ReviewedBy { get; set; }
        public User? ReviewedByUser { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
