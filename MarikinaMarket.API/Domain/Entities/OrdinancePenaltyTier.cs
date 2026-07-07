using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Domain.Entities
{
    public class OrdinancePenaltyTier
    {
        public int Id { get; set; }
        public int OrdinanceId { get; set; }
        public Ordinance? Ordinance { get; set; }
        public int OffenseNumber { get; set; }
        public Severity Severity { get; set; }
        public decimal PenaltyAmount { get; set; }
    }
}
