using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Ordinance.Response
{
    public class GetOrdinanceResponse
    {
        public required int Id { get; set; }
        public required string OrdinanceNo { get; set; }
        public required string Code { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public required ViolationCategory Category { get; set; }
        public required Severity Severity { get; set; }
        public required DateTime CreatedAt { get; set; }
    }
}
