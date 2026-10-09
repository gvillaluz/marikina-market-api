using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.DTOs.Backups.Response
{
    public class BackupResponse
    {
        public int Id { get; set; }
        public DateTime DateTime { get; set; }
        public BackupType Type { get; set; }
        public long Size { get; set; }
        public BackupStatus Status { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public string? ErrorMessage { get; set; }
        public bool CanDownload { get; set; }
        public string? DownloadRoute { get; set; }
    }
}
