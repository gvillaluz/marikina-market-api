using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarikinaMarket.API.Infrastructure.Repositories
{
    public class BackupRepository : IBackupRepository
    {
        private readonly AppDbContext _context;
        public BackupRepository(AppDbContext context) => _context = context;

        public async Task<BackupSchedule?> GetScheduleAsync(CancellationToken cancellationToken)
        {
            var tracked = _context.BackupSchedules.Local.SingleOrDefault(x => x.Id == 1);
            if (tracked != null)
            {
                // A backup can outlive a schedule edit made by another request scope.
                await _context.Entry(tracked).ReloadAsync(cancellationToken);
                return tracked;
            }
            return await _context.BackupSchedules.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        }

        public void AddSchedule(BackupSchedule schedule) => _context.BackupSchedules.Add(schedule);
        public void AddBackup(Backup backup) => _context.Backups.Add(backup);
        public Task<Backup?> GetByIdAsync(int id, CancellationToken cancellationToken)
            => _context.Backups.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<List<Backup>> GetHistoryAsync(int offset, int limit, CancellationToken cancellationToken)
            => _context.Backups.AsNoTracking().Where(x => x.IsFinalized)
                .OrderByDescending(x => x.DateTime).ThenByDescending(x => x.Id)
                .Skip(offset).Take(limit + 1).ToListAsync(cancellationToken);

        public Task<int> GetHistoryCountAsync(CancellationToken cancellationToken)
            => _context.Backups.CountAsync(x => x.IsFinalized, cancellationToken);

        public Task<Backup?> GetLatestAsync(bool completedOnly, CancellationToken cancellationToken)
            => _context.Backups.AsNoTracking().Where(x => x.IsFinalized &&
                    (!completedOnly || x.Status == BackupStatus.Completed))
                .OrderByDescending(x => x.DateTime).ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task RecoverInterruptedAsync(CancellationToken cancellationToken)
        {
            await _context.Backups.Where(x => !x.IsFinalized).ExecuteUpdateAsync(update => update
                .SetProperty(x => x.Status, BackupStatus.Failed)
                .SetProperty(x => x.IsFinalized, true)
                .SetProperty(x => x.ErrorMessage, "Backup was interrupted before completion."), cancellationToken);
        }

        public Task<Backup?> GetLatestAutomaticAsync(CancellationToken cancellationToken)
            => _context.Backups.AsNoTracking().Where(x => x.Type == BackupType.Automatic && x.IsFinalized)
                .OrderByDescending(x => x.DateTime).ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

        public async Task UpdateExpirationAsync(int retentionDays, CancellationToken cancellationToken)
        {
            await _context.Backups.Where(x => x.Status == BackupStatus.Completed && x.DeletedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(x => x.ExpiresAt,
                    x => x.CompletedAt!.Value.AddDays(retentionDays)), cancellationToken);
        }

        public Task<List<Backup>> GetCleanupCandidatesAsync(DateTime now, CancellationToken cancellationToken)
            => _context.Backups.Where(x => x.IsFinalized && x.DeletedAt == null &&
                (x.StorageCleanupPending || (x.Status == BackupStatus.Completed && x.ExpiresAt <= now)))
                .OrderBy(x => x.Id).ToListAsync(cancellationToken);

        public async Task SaveChangesAsync(CancellationToken cancellationToken)
            => await _context.SaveChangesAsync(cancellationToken);
    }
}
