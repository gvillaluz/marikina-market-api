namespace MarikinaMarket.API.Application.DTOs.Analytics.Internal
{
    public class PeakViolationTimeBucket
    {
        public int DayOfWeek { get; set; }
        public int TimeBlock { get; set; }
        public int Count { get; set; }
    }
}
