using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.DTOs.Vendor.Response
{
    public class VendorBusinessProfileResponse
    {
        public int VendorId { get; set; }
        public required string BusinessId { get; set; }
        public required string BusinessName { get; set; }
        public string? StallNumber { get; set; }
        public int MarketSectionId { get; set; }
        public string? MarketSectionName { get; set; }
        public VendorType Type { get; set; }
        public VendorStatus Status { get; set; }
        public int ComplianceScore { get; set; }
        public DateTime ScoreUpdatedAt { get; set; }
        public DateTime RegisteredAt { get; set; }
        public required string QrCodeValue { get; set; }

        public static VendorBusinessProfileResponse FromEntity(VendorProfile vendor) => new()
        {
            VendorId = vendor.Id,
            BusinessId = vendor.BusinessId,
            BusinessName = vendor.BusinessName,
            StallNumber = vendor.StallNumber,
            MarketSectionId = vendor.MarketSectionId,
            MarketSectionName = vendor.MarketSection?.Name,
            Type = vendor.Type,
            Status = vendor.Status,
            ComplianceScore = vendor.ComplianceScore,
            ScoreUpdatedAt = vendor.ScoreUpdatedAt,
            RegisteredAt = vendor.RegisteredAt,
            QrCodeValue = vendor.QrCodeValue
        };
    }
}
