using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Analytics.Internal
{
    public class ViolationCategoryBucket
    {
        public ViolationCategory Category { get; set; }
        public int Count { get; set; }
    }
}
