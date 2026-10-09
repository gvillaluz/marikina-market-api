using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Backups.Response
{
    public class BackupScheduleResponse
    {
        public bool Enabled { get; set; }
        public BackupFrequency Frequency { get; set; }
        public DayOfWeek? DayOfWeek { get; set; }
        public required string Time { get; set; }
        public int RetentionDays { get; set; }
        public string TimeZone { get; set; } = "Asia/Manila";
        public string TimeZoneDisplay { get; set; } = "Philippine Time (UTC+8)";
    }
}
