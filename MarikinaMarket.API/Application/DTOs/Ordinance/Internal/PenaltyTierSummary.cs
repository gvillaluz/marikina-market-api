using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Ordinance.Internal
{
    public class PenaltyTierSummary
    {
        public int OrdinanceId { get; set; }
        public int OffenseNumber { get; set; }
        public Severity Severity { get; set; }
        public decimal PenaltyAmount { get; set; }
    }
}
