namespace MarikinaMarket.API.Application.DTOs.Backups.Response
{
    public class BackupHealthResponse
    {
        public BackupResponse? LatestAttempt { get; set; }
        public BackupResponse? LatestSuccessfulBackup { get; set; }
        public bool IsRunning { get; set; }
        public bool Enabled { get; set; }
        public int RetentionDays { get; set; }
        public DateTime? NextScheduledAt { get; set; }
        public string TimeZone { get; set; } = "Asia/Manila";
        public string TimeZoneDisplay { get; set; } = "Philippine Time (UTC+8)";
    }
}
