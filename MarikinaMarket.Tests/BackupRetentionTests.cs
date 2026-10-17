using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Reflection;
using MarikinaMarket.API.Application;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;
using MarikinaMarket.API.Application.DTOs.Backups.Request;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using MarikinaMarket.API.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MarikinaMarket.Tests;

public class BackupRetentionTests
{
    [Fact]
    public async Task DownloadRejectsInvalidMissingAndUnavailableRecordsBeforeStorage()
    {
        var fixture = new BackupRetentionFixture();
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.DownloadAsync(0, default));
        await Assert.ThrowsAsync<RecordNotFoundException>(() => fixture.Service.DownloadAsync(999, default));
        foreach (var record in new[] {
            BackupRetentionFixture.Record(2, expires: DateTime.UtcNow.AddMinutes(-1)),
            BackupRetentionFixture.Record(3, status: BackupStatus.Failed),
            BackupRetentionFixture.Record(4, finalized: false),
            BackupRetentionFixture.Record(5, deleted: true) })
        {
            fixture.Records.Add(record);
            await Assert.ThrowsAsync<InvalidRequestException>(() => fixture.Service.DownloadAsync(record.Id, default));
        }
        Assert.Equal(0, fixture.Downloads);
        var file = await fixture.Service.DownloadAsync(1, default);
        using (file.Owner)
        {
            Assert.Equal("marikina-market-1-20261017-040000.mmbak", file.FileName);
            Assert.Equal(77, file.Stream.ReadByte());
        }
        Assert.Equal(1, fixture.Downloads);
    }

    [Theory]
    [InlineData("HeadAdmin", 200)]
    [InlineData("AdminOfficer", 200)]
    [InlineData("MarketVendor", 403)]
    [InlineData("MarketEnforcer", 403)]
    [InlineData(null, 401)]
    public async Task DownloadEnforcesExistingRolesAndNoStore(string? role, int expected)
    {
        var fixture = new BackupRetentionFixture();
        await using var host = await AuditHost.CreateAsync(services => services.AddSingleton<IBackupService>(fixture.Service));
        var response = await host.SendAsync(HttpMethod.Get, "/api/admin/backups/1/download", role);
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal(expected == 200 ? 1 : 0, fixture.Downloads);
        if (expected == 200)
        {
            Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
            Assert.Equal("application/octet-stream", response.Content.Headers.ContentType!.MediaType);
            Assert.Equal(new byte[] { 77, 77, 66, 75, 1, 0, 255 }, await response.Content.ReadAsByteArrayAsync());
        }
    }

    [Fact]
    public async Task RealRepositorySelectsOnlyExpiredOrCleanupPendingFinalizedRecords()
    {
        var now = new DateTime(2026, 10, 17, 4, 0, 0, DateTimeKind.Utc);
        var records = new[] {
            BackupRetentionFixture.Record(1, expires: now),
            BackupRetentionFixture.Record(2, expires: now.AddTicks(1)),
            BackupRetentionFixture.Record(3, expires: now.AddDays(-1), deleted: true),
            BackupRetentionFixture.Record(4, expires: now.AddDays(-1), finalized: false),
            BackupRetentionFixture.Record(5, expires: now.AddDays(-1), status: BackupStatus.Failed),
            BackupRetentionFixture.Record(6, expires: now.AddDays(1), status: BackupStatus.Failed) };
        records[5].StorageCleanupPending = true;
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        context.Backups = new ReportTestDbSet<Backup>(records);
        var candidates = await new BackupRepository(context).GetCleanupCandidatesAsync(now, default);
        Assert.Equal(new[] { 1, 6 }, candidates.Select(record => record.Id));
    }

    [Fact]
    public async Task CleanupFailureKeepsCandidateRetryableAndRetentionEditPreservesOccurrence()
    {
        // TEMP/TMP are isolated by the recorded test command before this process starts.
        var expectedRoot = Environment.GetEnvironmentVariable("BACKUP_TEST_TEMP_ROOT");
        Assert.False(string.IsNullOrEmpty(expectedRoot));
        Assert.Equal(Path.GetFullPath(expectedRoot!), Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar));
        var fixture = new BackupRetentionFixture();
        fixture.Records[0].ExpiresAt = DateTime.UtcNow.AddDays(-1);
        fixture.FailDelete = true;
        await fixture.Service.CleanupAsync(default);
        Assert.Null(fixture.Records[0].DeletedAt);
        fixture.FailDelete = false;
        await fixture.Service.CleanupAsync(default);
        Assert.NotNull(fixture.Records[0].DeletedAt);
        Assert.Equal(2, fixture.Deletes);
        var occurrence = fixture.Schedule.LastAttemptScheduledAt;
        var retry = fixture.Schedule.NextRetryAt;
        await fixture.Service.UpdateScheduleAsync(new UpdateBackupScheduleRequest {
            Enabled = true, Frequency = BackupFrequency.Daily, Time = "00:00", RetentionDays = 7 }, default);
        Assert.Equal(7, fixture.RetentionUpdated);
        Assert.Equal(occurrence, fixture.Schedule.LastAttemptScheduledAt);
        Assert.Equal(retry, fixture.Schedule.NextRetryAt);
    }
}

internal sealed class BackupRetentionFixture
{
    public List<Backup> Records { get; } = [Record(1)];
    public BackupSchedule Schedule { get; } = new() { UpdatedAt = DateTime.UtcNow.AddDays(-2), LastAttemptScheduledAt = DateTime.UtcNow.AddDays(-1), NextRetryAt = DateTime.UtcNow.AddMinutes(5) };
    public int Downloads, Deletes, RetentionUpdated;
    public bool FailDelete;
    public BackupService Service { get; }
    public BackupRetentionFixture()
    {
        var repository = BackupTestBoundary.Create<IBackupRepository>((name, args) => name switch {
            "GetByIdAsync" => Task.FromResult(Records.SingleOrDefault(row => row.Id == (int)args![0]!)),
            "GetScheduleAsync" => Task.FromResult<BackupSchedule?>(Schedule),
            "GetLatestAutomaticAsync" => Task.FromResult<Backup?>(null),
            "GetCleanupCandidatesAsync" => Task.FromResult(Records.Where(row => row.DeletedAt == null && row.IsFinalized && (row.StorageCleanupPending || row.ExpiresAt <= (DateTime)args![0]!)).ToList()),
            "UpdateExpirationAsync" => SetRetention((int)args![0]!),
            "SaveChangesAsync" or "RecoverInterruptedAsync" => Task.CompletedTask,
            _ => throw new InvalidOperationException($"Unexpected repository call: {name}") });
        var storage = BackupTestBoundary.Create<IStorageService>((name, args) => {
            Assert.Equal(B2BucketType.Backups, args![0]);
            Assert.Equal("synthetic/1.mmbak", args[1]);
            if (name == "DownloadStreamAsync") { Downloads++; var stream = new MemoryStream([77,77,66,75,1,0,255]); return Task.FromResult<(Stream, IDisposable)>((stream, stream)); }
            if (name == "DeleteAllVersionsAsync") { Deletes++; return FailDelete ? Task.FromException(new IOException("Synthetic storage outage")) : Task.CompletedTask; }
            throw new InvalidOperationException($"Unexpected storage call: {name}");
        });
        Service = new BackupService(repository, new ReportUnitOfWork(), storage, new ConfigurationBuilder().Build(),
            BackupTestBoundary.Create<IHostApplicationLifetime>((_, _) => CancellationToken.None), NullLogger<BackupService>.Instance,
            BackupTestBoundary.Create<IAuditLogService>((name, _) => name == "RecordAsync" ? Task.CompletedTask : throw new InvalidOperationException(name)), new AuditLogContext());
    }
    private Task SetRetention(int days) { RetentionUpdated = days; return Task.CompletedTask; }
    public static Backup Record(int id, DateTime? expires = null, BackupStatus status = BackupStatus.Completed, bool finalized = true, bool deleted = false) => new() {
        Id = id, B2Key = $"synthetic/{id}.mmbak", DateTime = new DateTime(2026,10,17,4,0,0,DateTimeKind.Utc), Status = status,
        IsFinalized = finalized, ExpiresAt = expires ?? DateTime.UtcNow.AddDays(1), DeletedAt = deleted ? DateTime.UtcNow : null };
}

public class BackupTestBoundary : DispatchProxy
{
    public Func<string, object?[]?, object?> Handler { get; set; } = null!;
    public static T Create<T>(Func<string, object?[]?, object?> handler) where T : class {
        var proxy = Create<T, BackupTestBoundary>();
        ((BackupTestBoundary)(object)proxy).Handler = handler;
        return proxy;
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!.Name, args);
}
