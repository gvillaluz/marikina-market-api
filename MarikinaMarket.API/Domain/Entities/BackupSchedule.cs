using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Domain.Entities
{
    public class BackupSchedule
    {
        public int Id { get; set; } = 1;
        public bool Enabled { get; set; } = true;
        public BackupFrequency Frequency { get; set; } = BackupFrequency.Daily;
        public DayOfWeek? DayOfWeek { get; set; }
        public TimeOnly Time { get; set; } = TimeOnly.MinValue;
        public int RetentionDays { get; set; } = 30;
        public DateTime UpdatedAt { get; set; }
        public DateTime? LastAttemptScheduledAt { get; set; }
        public DateTime? NextRetryAt { get; set; }
    }
}
