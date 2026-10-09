using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Ordinance.Response
{
    public class OrdinancePenaltyTierResponse
    {
        public int Id { get; set; }
        public int OffenseNumber { get; set; }
        public Severity Severity { get; set; }
        public decimal PenaltyAmount { get; set; }
    }
}
