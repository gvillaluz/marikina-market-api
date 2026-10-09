namespace MarikinaMarket.API.Application.DTOs.Ordinance.Response
{
    public class OrdinanceDetailResponse : OrdinanceSummaryResponse
    {
        public List<OrdinancePenaltyTierResponse> PenaltyTiers { get; set; } = [];
    }
}
