using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Domain.Entities
{
    public class Backup
    {
        public int Id { get; set; }
        public DateTime DateTime { get; set; }
        public BackupType Type { get; set; }
        public long Size { get; set; }
        public BackupStatus Status { get; set; }
        public required string B2Key { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public bool IsFinalized { get; set; }
        public bool StorageCleanupPending { get; set; }
    }
}
