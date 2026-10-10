using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using MarikinaMarket.API.Application.DTOs.Backups.Request;
using MarikinaMarket.API.Application.DTOs.Backups.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Npgsql;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;

namespace MarikinaMarket.API.Application.Services
{
    public class BackupService : IBackupService
    {
        private const int PAGE_SIZE = 10;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(5);
        private const long MaxDumpSize = 256L * 1024 * 1024;
        private static readonly byte[] EnvelopeHeader = [(byte)'M', (byte)'M', (byte)'B', (byte)'K', 1];
        private static readonly SemaphoreSlim ExecutionGate = new(1, 1);
        private static readonly SemaphoreSlim SettingsGate = new(1, 1);
        private static readonly TimeZoneInfo Manila = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
        private static readonly string TempRoot = Path.Combine(Path.GetTempPath(), "MarikinaMarket-backups");
        private static bool _initialized;
        private static int _isRunning;
        private readonly IBackupRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStorageService _storage;
        private readonly IConfiguration _configuration;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<BackupService> _logger;
        private readonly IAuditLogService _auditService;
        private readonly AuditLogContext _audit;

        public BackupService(IBackupRepository repository, IUnitOfWork unitOfWork,
            IStorageService storage, IConfiguration configuration, IHostApplicationLifetime lifetime,
            ILogger<BackupService> logger, IAuditLogService auditService, AuditLogContext audit)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _storage = storage;
            _configuration = configuration;
            _lifetime = lifetime;
            _logger = logger;
            _auditService = auditService;
            _audit = audit;
        }

        public async Task<BackupResponse> CreateManualAsync(CancellationToken cancellationToken)
        {
            if (!await ExecutionGate.WaitAsync(0, cancellationToken))
                throw new ConcurrencyConflictException("A backup or backup maintenance operation is already running.");
            try
            {
                await InitializeCoreAsync(cancellationToken);
                var attempt = (await StartAttemptAsync(BackupType.Manual, cancellationToken))!.Value;
                return await ExecuteBackupAsync(attempt.Backup, attempt.Occurrence, attempt.ScheduleUpdatedAt, cancellationToken);
            }
            finally { ExecutionGate.Release(); }
        }

        public async Task RunScheduledAsync(CancellationToken cancellationToken)
        {
            if (!await ExecutionGate.WaitAsync(0, cancellationToken)) return;
            try
            {
                await InitializeCoreAsync(cancellationToken);
                var attempt = await StartAttemptAsync(BackupType.Automatic, cancellationToken);
                if (attempt.HasValue)
                    await ExecuteBackupAsync(attempt.Value.Backup, attempt.Value.Occurrence, attempt.Value.ScheduleUpdatedAt, cancellationToken);
            }
            finally { ExecutionGate.Release(); }
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await ExecutionGate.WaitAsync(cancellationToken);
            try { await InitializeCoreAsync(cancellationToken); }
            finally { ExecutionGate.Release(); }
        }

        private async Task InitializeCoreAsync(CancellationToken cancellationToken)
        {
            if (_initialized) return;
            await _repository.RecoverInterruptedAsync(cancellationToken);
            await SettingsGate.WaitAsync(cancellationToken);
            try
            {
                var schedule = await _repository.GetScheduleAsync(cancellationToken);
                if (schedule != null && schedule.Enabled && schedule.NextRetryAt == null &&
                    schedule.LastAttemptScheduledAt >= schedule.UpdatedAt)
                {
                    var latest = await _repository.GetLatestAutomaticAsync(cancellationToken);
                    if (latest != null && latest.Status == BackupStatus.Failed &&
                        latest.DateTime >= schedule.LastAttemptScheduledAt && latest.DateTime >= schedule.UpdatedAt)
                    {
                        schedule.NextRetryAt = DateTime.UtcNow.Add(RetryDelay);
                        await _repository.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            finally { SettingsGate.Release(); }
            // Only our application's temporary dump files are recovered; never recurse into other directories.
            CleanupTemporaryFiles();
            _initialized = true;
        }

        private static void CleanupTemporaryFiles()
        {
            if (!Directory.Exists(TempRoot)) return;
            foreach (var path in Directory.EnumerateFiles(TempRoot, "*.tmp", SearchOption.TopDirectoryOnly))
                File.Delete(path);
        }

        private async Task<BackupSchedule> GetOrCreateScheduleAsync(CancellationToken cancellationToken)
        {
            var schedule = await _repository.GetScheduleAsync(cancellationToken);
            if (schedule != null) return schedule;
            schedule = new BackupSchedule { UpdatedAt = DateTime.UtcNow };
            _repository.AddSchedule(schedule);
            await _repository.SaveChangesAsync(cancellationToken);
            return schedule;
        }

        private async Task<(Backup Backup, DateTime? Occurrence, DateTime ScheduleUpdatedAt)?> StartAttemptAsync(
            BackupType type, CancellationToken cancellationToken)
        {
            await SettingsGate.WaitAsync(cancellationToken);
            try
            {
                var schedule = await GetOrCreateScheduleAsync(cancellationToken);
                var now = DateTime.UtcNow;
                var occurrence = LatestOccurrence(schedule, now);
                if (type == BackupType.Automatic && (!schedule.Enabled || occurrence < schedule.UpdatedAt ||
                    (schedule.LastAttemptScheduledAt.HasValue && occurrence < schedule.LastAttemptScheduledAt) ||
                    (occurrence == schedule.LastAttemptScheduledAt &&
                        (!schedule.NextRetryAt.HasValue || schedule.NextRetryAt > now))))
                    return null;

                var backup = new Backup
                {
                    DateTime = now,
                    Type = type,
                    Status = BackupStatus.Failed,
                    B2Key = $"backups/{now:yyyy/MM/dd}/{Guid.NewGuid():N}.mmbak",
                    // Persist before upload so interrupted/ambiguous uploads can be removed on restart.
                    StorageCleanupPending = true
                };
                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    if (type == BackupType.Automatic)
                    {
                        schedule.LastAttemptScheduledAt = occurrence;
                        schedule.NextRetryAt = now.Add(RetryDelay);
                    }
                    _repository.AddBackup(backup);
                    await _repository.SaveChangesAsync(cancellationToken);
                    await _unitOfWork.CommitAsync();
                }
                catch
                {
                    await _unitOfWork.RollbackAsync();
                    throw;
                }
                return (backup, type == BackupType.Automatic ? occurrence : null, schedule.UpdatedAt);
            }
            finally { SettingsGate.Release(); }
        }

        private async Task<BackupResponse> ExecuteBackupAsync(Backup backup, DateTime? occurrence,
            DateTime scheduleUpdatedAt, CancellationToken cancellationToken)
        {
            Interlocked.Exchange(ref _isRunning, 1);
            var tempId = Guid.NewGuid().ToString("N");
            var dumpPath = Path.Combine(TempRoot, tempId + ".dump.tmp");
            var encryptedPath = Path.Combine(TempRoot, tempId + ".encrypted.tmp");
            string stage = "configuration";
            byte[]? key = null;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.ApplicationStopping);
            try
            {
                key = ReadEncryptionKey();
                var timeoutMinutes = _configuration.GetValue<int?>("Backups:TimeoutMinutes") ?? 15;
                if (timeoutMinutes is < 1 or > 120) throw new InvalidOperationException("Invalid backup timeout.");
                timeout.CancelAfter(TimeSpan.FromMinutes(timeoutMinutes));
                Directory.CreateDirectory(TempRoot);
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(TempRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

                stage = "database dump";
                await CreateDumpAsync(dumpPath, timeout.Token);
                stage = "encryption";
                await EncryptDumpAsync(dumpPath, encryptedPath, key, timeout.Token);
                // A failure to remove plaintext prevents upload.
                File.Delete(dumpPath);
                stage = "storage upload";
                await using (var encrypted = File.OpenRead(encryptedPath))
                    await _storage.UploadStreamAsync(B2BucketType.Backups, encrypted, backup.B2Key, timeout.Token);

                stage = "history persistence";
                await SettingsGate.WaitAsync(timeout.Token);
                try
                {
                    var schedule = await _repository.GetScheduleAsync(timeout.Token)
                        ?? throw new InvalidOperationException("Backup schedule is missing.");
                    backup.Size = new FileInfo(encryptedPath).Length;
                    backup.CompletedAt = DateTime.UtcNow;
                    backup.ExpiresAt = backup.CompletedAt.Value.AddDays(schedule.RetentionDays);
                    backup.Status = BackupStatus.Completed;
                    backup.IsFinalized = true;
                    backup.StorageCleanupPending = false;
                    backup.ErrorMessage = null;
                    if (occurrence.HasValue && schedule.UpdatedAt == scheduleUpdatedAt &&
                        schedule.LastAttemptScheduledAt == occurrence)
                        schedule.NextRetryAt = null;
                    await _repository.SaveChangesAsync(timeout.Token);
                }
                finally { SettingsGate.Release(); }
            }
            catch (Exception ex)
            {
                var error = ex is OperationCanceledException
                    ? "Backup was cancelled or exceeded its time limit."
                    : $"Backup failed during {stage}.";
                await RecordFailureAsync(backup, error, occurrence, scheduleUpdatedAt);
                if (backup.Type == BackupType.Automatic)
                    await RecordBackupAuditAsync(backup.Id, "CreateAutomaticBackup", LogResult.Failed, "System automatic backup failed.");
                // Do not attach provider exceptions: they can include credentials or database diagnostics.
                _logger.LogWarning("Backup {BackupId} failed during {Stage} ({ExceptionType}).", backup.Id, stage, ex.GetType().Name);
                if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException("Backup was cancelled.", cancellationToken);
                throw new InvalidOperationException(error);
            }
            finally
            {
                if (key != null) CryptographicOperations.ZeroMemory(key);
                DeleteTemporaryFile(dumpPath);
                DeleteTemporaryFile(encryptedPath);
                Interlocked.Exchange(ref _isRunning, 0);
            }

            if (backup.Type == BackupType.Automatic)
                await RecordBackupAuditAsync(backup.Id, "CreateAutomaticBackup", LogResult.Success, "System automatic backup completed.");
            return ToResponse(backup);
        }

        private byte[] ReadEncryptionKey()
        {
            var encoded = _configuration["Backups:EncryptionKey"];
            if (string.IsNullOrWhiteSpace(encoded)) throw new InvalidOperationException("Backup encryption key is missing.");
            var key = Convert.FromBase64String(encoded);
            if (key.Length == 32) return key;
            CryptographicOperations.ZeroMemory(key);
            throw new InvalidOperationException("Backup encryption key must contain 32 bytes.");
        }

        private async Task CreateDumpAsync(string path, CancellationToken cancellationToken)
        {
            var connection = new NpgsqlConnectionStringBuilder(_configuration.GetConnectionString("DefaultConnection"));
            var start = new ProcessStartInfo
            {
                FileName = _configuration["Backups:PgDumpPath"] ?? "pg_dump",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            start.ArgumentList.Add("--format=custom");
            start.ArgumentList.Add("--no-password");
            start.ArgumentList.Add("--file");
            start.ArgumentList.Add(path);
            start.Environment["PGHOST"] = connection.Host;
            start.Environment["PGPORT"] = connection.Port.ToString(CultureInfo.InvariantCulture);
            start.Environment["PGDATABASE"] = connection.Database;
            start.Environment["PGUSER"] = connection.Username;
            start.Environment["PGPASSWORD"] = connection.Password;
            start.Environment["PGSSLMODE"] = connection.SslMode switch
            {
                SslMode.VerifyCA => "verify-ca",
                SslMode.VerifyFull => "verify-full",
                _ => connection.SslMode.ToString().ToLowerInvariant()
            };
            if (!string.IsNullOrWhiteSpace(connection.RootCertificate)) start.Environment["PGSSLROOTCERT"] = connection.RootCertificate;
            using var process = new Process { StartInfo = start };
            var started = false;
            Task? stderr = null;
            Task? stdout = null;
            try
            {
                if (!process.Start()) throw new InvalidOperationException("Database dump could not start.");
                started = true;
                // Drain without retaining diagnostics in memory or exposing them in logs.
                stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
                stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, cancellationToken);
                await process.WaitForExitAsync(cancellationToken);
                await Task.WhenAll(stderr, stdout);
                if (process.ExitCode != 0) throw new InvalidOperationException("Database dump failed.");
            }
            finally
            {
                if (started && !process.HasExited)
                {
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) when (process.HasExited) { }
                    await process.WaitForExitAsync(CancellationToken.None);
                }
                if (stderr != null && stdout != null)
                {
                    try { await Task.WhenAll(stderr, stdout); }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
                }
                // Remove the password from the managed process configuration as soon as possible.
                start.Environment.Remove("PGPASSWORD");
            }
            var size = new FileInfo(path).Length;
            if (size <= 0 || size > MaxDumpSize) throw new InvalidOperationException("Database dump is empty or exceeds 256 MiB.");
        }

        private static async Task EncryptDumpAsync(string dumpPath, string encryptedPath, byte[] key, CancellationToken cancellationToken)
        {
            // Validate again before allocating either whole-file buffer.
            var length = new FileInfo(dumpPath).Length;
            if (length <= 0 || length > MaxDumpSize) throw new InvalidOperationException("Invalid database dump size.");
            byte[]? plaintext = null;
            byte[]? ciphertext = null;
            try
            {
                plaintext = await File.ReadAllBytesAsync(dumpPath, cancellationToken);
                ciphertext = new byte[plaintext.Length];
                var nonce = RandomNumberGenerator.GetBytes(12);
                var tag = new byte[16];
                using (var aes = new AesGcm(key, 16))
                    aes.Encrypt(nonce, plaintext, ciphertext, tag, EnvelopeHeader);
                cancellationToken.ThrowIfCancellationRequested();
                await using var output = new FileStream(encryptedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    81920, FileOptions.Asynchronous);
                await output.WriteAsync(EnvelopeHeader, cancellationToken);
                await output.WriteAsync(nonce, cancellationToken);
                await output.WriteAsync(tag, cancellationToken);
                await output.WriteAsync(ciphertext, cancellationToken);
            }
            finally
            {
                if (plaintext != null) CryptographicOperations.ZeroMemory(plaintext);
                if (ciphertext != null) CryptographicOperations.ZeroMemory(ciphertext);
            }
        }

        private async Task RecordFailureAsync(Backup backup, string error, DateTime? occurrence, DateTime scheduleUpdatedAt)
        {
            using var storageRecovery = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            backup.Status = BackupStatus.Failed;
            backup.IsFinalized = true;
            backup.CompletedAt = null;
            backup.ExpiresAt = null;
            backup.Size = 0;
            backup.ErrorMessage = error;
            backup.StorageCleanupPending = true;
            try
            {
                await _storage.DeleteAllVersionsAsync(B2BucketType.Backups, backup.B2Key, storageRecovery.Token);
                backup.StorageCleanupPending = false;
                backup.DeletedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Backup {BackupId} storage cleanup will be retried ({ExceptionType}).", backup.Id, ex.GetType().Name);
            }
            using var historyRecovery = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                await SettingsGate.WaitAsync(historyRecovery.Token);
                try
                {
                    if (occurrence.HasValue)
                    {
                        var schedule = await _repository.GetScheduleAsync(historyRecovery.Token);
                        if (schedule != null && schedule.Enabled && schedule.UpdatedAt == scheduleUpdatedAt &&
                            schedule.LastAttemptScheduledAt == occurrence)
                            schedule.NextRetryAt = DateTime.UtcNow.Add(RetryDelay);
                    }
                    await _repository.SaveChangesAsync(historyRecovery.Token);
                }
                finally { SettingsGate.Release(); }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Backup {BackupId} failure recording requires recovery ({ExceptionType}).", backup.Id, ex.GetType().Name);
            }
        }

        private void DeleteTemporaryFile(string path)
        {
            try { File.Delete(path); }
            catch (Exception ex)
            {
                _logger.LogWarning("A backup temporary file could not be removed; startup cleanup will retry ({ExceptionType}).", ex.GetType().Name);
            }
        }

        public async Task CleanupAsync(CancellationToken cancellationToken)
        {
            if (!await ExecutionGate.WaitAsync(0, cancellationToken)) return;
            try
            {
                await InitializeCoreAsync(cancellationToken);
                await CleanupCoreAsync(cancellationToken);
            }
            finally { ExecutionGate.Release(); }
        }

        private async Task CleanupCoreAsync(CancellationToken cancellationToken)
        {
            CleanupTemporaryFiles();
            await SettingsGate.WaitAsync(cancellationToken);
            try
            {
                // Keep retention changes atomic with selecting and deleting expired objects.
                foreach (var backup in await _repository.GetCleanupCandidatesAsync(DateTime.UtcNow, cancellationToken))
                {
                    var previousCleanupPending = backup.StorageCleanupPending;
                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        timeout.CancelAfter(TimeSpan.FromSeconds(30));
                        await _storage.DeleteAllVersionsAsync(B2BucketType.Backups, backup.B2Key, timeout.Token);
                        backup.DeletedAt = DateTime.UtcNow;
                        backup.StorageCleanupPending = false;
                        await _repository.SaveChangesAsync(timeout.Token);
                        await RecordBackupAuditAsync(backup.Id, "CleanupBackup", LogResult.Success, "Expired backup was removed from storage.");
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                    {
                        backup.DeletedAt = null;
                        backup.StorageCleanupPending = previousCleanupPending;
                        await RecordBackupAuditAsync(backup.Id, "CleanupBackup", LogResult.Failed, "Backup storage cleanup failed and will be retried.");
                        _logger.LogWarning("Backup {BackupId} retention cleanup will be retried ({ExceptionType}).", backup.Id, ex.GetType().Name);
                    }
                }
            }
            finally { SettingsGate.Release(); }
        }

        private Task RecordBackupAuditAsync(int id, string action, LogResult result, string details)
            => _auditService.RecordAsync(new AuditLog
            {
                UserId = _audit.Entry?.UserId, Role = _audit.Entry?.Role,
                Action = action, Module = Module.Backups,
                TargetId = id.ToString(CultureInfo.InvariantCulture), Result = result, Details = details
            });

        public async Task<BackupScheduleResponse> GetScheduleAsync(CancellationToken cancellationToken)
        {
            await SettingsGate.WaitAsync(cancellationToken);
            try { return ToScheduleResponse(await GetOrCreateScheduleAsync(cancellationToken)); }
            finally { SettingsGate.Release(); }
        }

        public async Task<BackupScheduleResponse> UpdateScheduleAsync(UpdateBackupScheduleRequest request, CancellationToken cancellationToken)
        {
            var time = ValidateSchedule(request);
            await SettingsGate.WaitAsync(cancellationToken);
            try
            {
                var schedule = await GetOrCreateScheduleAsync(cancellationToken);
                await _unitOfWork.BeginTransactionAsync();
                try
                {
                    var schedulingChanged = schedule.Enabled != request.Enabled || schedule.Frequency != request.Frequency ||
                        schedule.DayOfWeek != request.DayOfWeek || schedule.Time != time;
                    schedule.Enabled = request.Enabled!.Value;
                    schedule.Frequency = request.Frequency!.Value;
                    schedule.DayOfWeek = request.DayOfWeek;
                    schedule.Time = time;
                    schedule.RetentionDays = request.RetentionDays!.Value;
                    // Retention-only edits must not erase a pending scheduled occurrence.
                    if (schedulingChanged)
                    {
                        schedule.UpdatedAt = DateTime.UtcNow;
                        schedule.LastAttemptScheduledAt = null;
                        schedule.NextRetryAt = null;
                    }
                    await _repository.UpdateExpirationAsync(schedule.RetentionDays, cancellationToken);
                    await _repository.SaveChangesAsync(cancellationToken);
                    await _unitOfWork.CommitAsync();
                }
                catch
                {
                    await _unitOfWork.RollbackAsync();
                    throw;
                }
                return ToScheduleResponse(schedule);
            }
            finally { SettingsGate.Release(); }
        }

        private static TimeOnly ValidateSchedule(UpdateBackupScheduleRequest request)
        {
            if (!request.Enabled.HasValue || !request.Frequency.HasValue || !Enum.IsDefined(request.Frequency.Value))
                throw new ValidationException("Enabled and a valid Daily or Weekly frequency are required.");
            if (request.Frequency == BackupFrequency.Weekly &&
                (!request.DayOfWeek.HasValue || !Enum.IsDefined(request.DayOfWeek.Value)))
                throw new ValidationException("A valid day of week is required for weekly backups.");
            if (request.Frequency == BackupFrequency.Daily && request.DayOfWeek.HasValue)
                throw new ValidationException("Day of week must be omitted for daily backups.");
            if (request.Time == null || request.Time.Length != 5 ||
                !TimeOnly.TryParseExact(request.Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                throw new ValidationException("Time must use HH:mm in Philippine Time.");
            if (!request.RetentionDays.HasValue || request.RetentionDays is < 1 or > 365)
                throw new ValidationException("Retention must be between 1 and 365 days.");
            return time;
        }

        private static DateTime LatestOccurrence(BackupSchedule schedule, DateTime now)
        {
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, Manila);
            var candidate = DateTime.SpecifyKind(localNow.Date.Add(schedule.Time.ToTimeSpan()), DateTimeKind.Unspecified);
            if (schedule.Frequency == BackupFrequency.Weekly)
            {
                var days = ((int)candidate.DayOfWeek - (int)schedule.DayOfWeek!.Value + 7) % 7;
                candidate = candidate.AddDays(-days);
                if (candidate > localNow) candidate = candidate.AddDays(-7);
            }
            else if (candidate > localNow) candidate = candidate.AddDays(-1);
            return TimeZoneInfo.ConvertTimeToUtc(candidate, Manila);
        }

        private static DateTime? NextOccurrence(BackupSchedule schedule, DateTime now)
        {
            if (!schedule.Enabled) return null;
            var latest = LatestOccurrence(schedule, now);
            if (latest >= schedule.UpdatedAt &&
                (!schedule.LastAttemptScheduledAt.HasValue || latest > schedule.LastAttemptScheduledAt))
                return latest; // Overdue work is still the next backup, including a catch-up after downtime.
            var next = latest.AddDays(schedule.Frequency == BackupFrequency.Daily ? 1 : 7);
            if (latest >= schedule.UpdatedAt && latest == schedule.LastAttemptScheduledAt &&
                schedule.NextRetryAt.HasValue && schedule.NextRetryAt.Value < next)
                return schedule.NextRetryAt.Value;
            return next;
        }

        public async Task<PageResponse<BackupResponse>> GetHistoryAsync(int offset, CancellationToken cancellationToken)
        {
            if (offset < 0) throw new ValidationException("Offset cannot be negative.");
            var backups = await _repository.GetHistoryAsync(offset, PAGE_SIZE, cancellationToken);
            return new PageResponse<BackupResponse>
            {
                Items = backups.Take(PAGE_SIZE).Select(ToResponse).ToList(),
                HasMore = backups.Count > PAGE_SIZE,
                Total = await _repository.GetHistoryCountAsync(cancellationToken)
            };
        }

        public async Task<BackupHealthResponse> GetHealthAsync(CancellationToken cancellationToken)
        {
            await SettingsGate.WaitAsync(cancellationToken);
            try
            {
                var schedule = await GetOrCreateScheduleAsync(cancellationToken);
                var latest = await _repository.GetLatestAsync(false, cancellationToken);
                var completed = await _repository.GetLatestAsync(true, cancellationToken);
                return new BackupHealthResponse
                {
                    LatestAttempt = latest == null ? null : ToResponse(latest),
                    LatestSuccessfulBackup = completed == null ? null : ToResponse(completed),
                    IsRunning = Volatile.Read(ref _isRunning) == 1,
                    Enabled = schedule.Enabled,
                    RetentionDays = schedule.RetentionDays,
                    NextScheduledAt = NextOccurrence(schedule, DateTime.UtcNow)
                };
            }
            finally { SettingsGate.Release(); }
        }

        public async Task<(Stream Stream, string FileName, IDisposable Owner)> DownloadAsync(int id, CancellationToken cancellationToken)
        {
            if (id <= 0) throw new ValidationException("Backup ID must be positive.");
            var backup = await _repository.GetByIdAsync(id, cancellationToken)
                ?? throw new RecordNotFoundException("Backup was not found.");
            if (!ToResponse(backup).CanDownload) throw new InvalidRequestException("This backup is not available for download.");
            var file = await _storage.DownloadStreamAsync(B2BucketType.Backups, backup.B2Key, cancellationToken);
            return (file.Stream, $"marikina-market-{backup.Id}-{backup.DateTime:yyyyMMdd-HHmmss}.mmbak", file.Owner);
        }

        private static BackupResponse ToResponse(Backup backup)
        {
            var canDownload = backup.IsFinalized && backup.Status == BackupStatus.Completed &&
                backup.DeletedAt == null && backup.ExpiresAt > DateTime.UtcNow;
            return new BackupResponse
            {
                Id = backup.Id, DateTime = backup.DateTime, Type = backup.Type, Size = backup.Size,
                Status = backup.Status, CompletedAt = backup.CompletedAt, ExpiresAt = backup.ExpiresAt,
                ErrorMessage = backup.ErrorMessage, CanDownload = canDownload,
                DownloadRoute = canDownload ? $"/api/admin/backups/{backup.Id}/download" : null
            };
        }

        private static BackupScheduleResponse ToScheduleResponse(BackupSchedule schedule) => new()
        {
            Enabled = schedule.Enabled, Frequency = schedule.Frequency, DayOfWeek = schedule.DayOfWeek,
            Time = schedule.Time.ToString("HH:mm", CultureInfo.InvariantCulture), RetentionDays = schedule.RetentionDays
        };
    }
}
