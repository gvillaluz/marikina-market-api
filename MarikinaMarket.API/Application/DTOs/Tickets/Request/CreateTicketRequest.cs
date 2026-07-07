using MarikinaMarket.API.Domain.Enums;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Request
{
    public class CreateTicketRequest
    {
        [Required(ErrorMessage = "Control number is required.")]
        [MaxLength(5), MinLength(5)]
        public required string ControlNumber { get; set; }

        [Required(ErrorMessage = "Vendor's information is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Vendor's information is required.")]
        public int VendorId { get; set; }

        [Required(ErrorMessage = "Market section is required.")]
        public int MarketSectionId { get; set; }

        [Required(ErrorMessage = "Enforcer's information is required.")]
        public int EnforcerId { get; set; }

        [Required(ErrorMessage = "Ticket type is required.")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public TicketType Type { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        public required string Description { get; set; }

        [Required(ErrorMessage = "Severity is required.")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Severity HighestSeverity { get; set; }

        [Required(ErrorMessage = "Penalty type is required.")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PenaltyType PenaltyType { get; set; }

        public int? CommunityServiceHours { get; set; }

        [Required(ErrorMessage = "Primary category is required.")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ViolationCategory PrimaryCategory { get; set; }

        [Required(ErrorMessage = "Issued ticket date is required.")]
        public DateTime IssuedAt { get; set; }

        [Required(ErrorMessage = "At least one ordinance is required..")]
        public required List<int> Ordinances { get; set; } = [];

        [Required]
        [MinLength(1, ErrorMessage = "Ticket evidence is required.")]
        public List<TicketEvidenceItem> TicketEvidenceUrls { get; set; } = [];
    }
}
