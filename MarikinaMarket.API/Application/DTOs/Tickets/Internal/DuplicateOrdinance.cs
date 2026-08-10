namespace MarikinaMarket.API.Application.DTOs.Tickets.Internal
{
    public class DuplicateOrdinance
    {
        public int OrdinanceId { get; set; }
        public required string OrdinanceNo { get; set; }
        public required string OrdinanceCode { get; set; }
    }
}
