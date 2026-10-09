using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Ordinance.Request
{
    public class SavePenaltyTierRequest
    {
        public required int OffenseNumber { get; set; }
        public required Severity Severity { get; set; }
        public required decimal PenaltyAmount { get; set; }
    }
}
