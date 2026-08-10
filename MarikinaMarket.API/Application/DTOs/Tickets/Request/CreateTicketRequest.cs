using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MarikinaMarket.API.Application.DTOs.Tickets.Request
{
    public class CreateTicketRequest
    {
        [FromForm(Name = "vendor_id")]
        [Required(ErrorMessage = "Vendor's information is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Vendor's information is required.")]
        public int VendorId { get; set; }

        [FromForm(Name = "market_section_id")]
        [Required(ErrorMessage = "Market section is required.")]
        public int MarketSectionId { get; set; }

        [FromForm(Name = "enforcer_id")]
        [Required(ErrorMessage = "Enforcer's information is required.")]
        public int EnforcerId { get; set; }

        [FromForm(Name = "type")]
        [Required(ErrorMessage = "Ticket type is required.")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ViolationType Type { get; set; }

        [FromForm(Name = "description")]
        [Required(ErrorMessage = "Description is required.")]
        public required string Description { get; set; }

        [FromForm(Name = "penalty_type")]
        [Required(ErrorMessage = "Penalty type is required.")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public PenaltyType PenaltyType { get; set; }

        [FromForm(Name = "community_service_hours")]
        public int? CommunityServiceHours { get; set; }

        [FromForm(Name = "ordinances")]
        [Required(ErrorMessage = "At least one ordinance is required..")]
        public required List<int> Ordinances { get; set; } = [];

        [FromForm(Name = "ticket_evidence_files")]
        public List<IFormFile> TicketEvidenceFiles { get; set; } = [];
    }
}
