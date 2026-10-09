using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface IBackupRepository
    {
        Task<BackupSchedule?> GetScheduleAsync(CancellationToken cancellationToken);
        void AddSchedule(BackupSchedule schedule);
        void AddBackup(Backup backup);
        Task<Backup?> GetByIdAsync(int id, CancellationToken cancellationToken);
        Task<List<Backup>> GetHistoryAsync(int offset, int limit, CancellationToken cancellationToken);
        Task<int> GetHistoryCountAsync(CancellationToken cancellationToken);
        Task<Backup?> GetLatestAsync(bool completedOnly, CancellationToken cancellationToken);
        Task<Backup?> GetLatestAutomaticAsync(CancellationToken cancellationToken);
        Task RecoverInterruptedAsync(CancellationToken cancellationToken);
        Task UpdateExpirationAsync(int retentionDays, CancellationToken cancellationToken);
        Task<List<Backup>> GetCleanupCandidatesAsync(DateTime now, CancellationToken cancellationToken);
        Task SaveChangesAsync(CancellationToken cancellationToken);
    }
}
