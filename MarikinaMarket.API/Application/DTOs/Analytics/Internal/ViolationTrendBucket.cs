using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Analytics.Internal
{
    public class ViolationTrendBucket
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public Severity? Severity { get; set; }
        public int Count { get; set; }
    }
}
