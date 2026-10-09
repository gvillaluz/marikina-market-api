using MarikinaMarket.API.Application.DTOs.Backups.Request;
using MarikinaMarket.API.Application.DTOs.Backups.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IBackupService
    {
        Task<BackupResponse> CreateManualAsync(CancellationToken cancellationToken);
        Task<PageResponse<BackupResponse>> GetHistoryAsync(int offset, CancellationToken cancellationToken);
        Task<BackupScheduleResponse> GetScheduleAsync(CancellationToken cancellationToken);
        Task<BackupScheduleResponse> UpdateScheduleAsync(UpdateBackupScheduleRequest request, CancellationToken cancellationToken);
        Task<BackupHealthResponse> GetHealthAsync(CancellationToken cancellationToken);
        Task<(Stream Stream, string FileName, IDisposable Owner)> DownloadAsync(int id, CancellationToken cancellationToken);
        Task InitializeAsync(CancellationToken cancellationToken);
        Task RunScheduledAsync(CancellationToken cancellationToken);
        Task CleanupAsync(CancellationToken cancellationToken);
    }
}
