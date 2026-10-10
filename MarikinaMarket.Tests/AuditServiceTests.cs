using System.ComponentModel.DataAnnotations;
using System.Reflection;
using MarikinaMarket.API.Application;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;
using MarikinaMarket.API.Application.DTOs.Audits.Request;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Migrations;
using MarikinaMarket.API.Infrastructure.Persistence;
using MarikinaMarket.API.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Module = MarikinaMarket.API.Domain.Enums.Module;

namespace MarikinaMarket.Tests;

public class AuditServiceTests
{
    private static ServiceProvider Provider(MemoryAuditRepository repository, IUserRepository? users = null)
        => new ServiceCollection().AddLogging().AddSingleton<IAuditLogRepository>(repository)
            .AddSingleton<IUserRepository>(users ?? AuditUserRepositoryStub.Create())
            .AddScoped<AuditLogContext>().AddScoped<IAuditLogService, AuditLogService>().BuildServiceProvider();

    private static AuditLog Log(int id = 0) => new()
    {
        Id = id, Timestamp = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc),
        UserId = 7, Role = Role.HeadAdmin, Action = "UpdateProfile", Module = Module.Users,
        TargetId = "7", Result = LogResult.Success, Details = "User updated a profile."
    };

    [Fact]
    public async Task Paging_uses_ten_items_extra_row_total_and_deterministic_order()
    {
        var repository = new MemoryAuditRepository();
        repository.Logs.AddRange(Enumerable.Range(1, 12).Select(Log));
        using var provider = Provider(repository);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        var first = await service.GetSummariesAsync(0, new());
        Assert.Equal(10, first.Items.Count);
        Assert.True(first.HasMore);
        Assert.Equal(12, first.Total);
        Assert.Equal(Enumerable.Range(3, 10).Reverse(), first.Items.Select(x => x.Id));
        Assert.Equal(10, repository.Limit);
        var last = await service.GetSummariesAsync(10, new());
        Assert.Equal(new[] { 2, 1 }, last.Items.Select(x => x.Id));
        Assert.False(last.HasMore);
        Assert.Equal(12, last.Total);
    }

    [Fact]
    public async Task Summary_batches_only_distinct_actor_IDs_from_the_visible_page_and_keeps_order()
    {
        var repository = new MemoryAuditRepository();
        repository.Logs.AddRange(Enumerable.Range(1, 11).Select(Log));
        repository.Logs[0].UserId = 999;
        repository.Logs[9].UserId = 8;
        repository.Logs[9].Role = Role.MarketEnforcer;
        var users = AuditUserRepositoryStub.Create();
        var stub = (AuditUserRepositoryStub)(object)users;
        using var provider = Provider(repository, users);
        using var scope = provider.CreateScope();
        var page = await scope.ServiceProvider.GetRequiredService<IAuditLogService>().GetSummariesAsync(0, new());
        Assert.Equal(new[] { 7, 8 }, Assert.Single(stub.NameRequests).Order());
        Assert.Equal(10, page.Items.Count);
        Assert.True(page.HasMore);
        Assert.Equal(11, page.Total);
        Assert.Equal(11, page.Items[0].Id);
        Assert.Equal("Juan", page.Items[0].FirstName);
        Assert.Equal("Dela Cruz", page.Items[0].LastName);
        Assert.Equal(10, page.Items[1].Id);
        Assert.Equal("Maria", page.Items[1].FirstName);
        Assert.Equal("Santos", page.Items[1].LastName);
        Assert.Equal(Role.MarketEnforcer, page.Items[1].Role);
    }

    [Fact]
    public async Task Names_are_current_and_missing_accounts_keep_historical_ID_role_and_event()
    {
        var repository = new MemoryAuditRepository();
        repository.Logs.Add(Log(1));
        var missing = Log(2); missing.UserId = 999; repository.Logs.Add(missing);
        var system = Log(3); system.UserId = null; system.Role = null; repository.Logs.Add(system);
        var users = AuditUserRepositoryStub.Create();
        var stub = (AuditUserRepositoryStub)(object)users;
        using var provider = Provider(repository, users);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        var first = await service.GetSummariesAsync(0, new());
        Assert.Null(first.Items[0].FirstName);
        Assert.Null(first.Items[0].LastName);
        Assert.Equal(999, first.Items[1].UserId);
        Assert.Equal(Role.HeadAdmin, first.Items[1].Role);
        Assert.Null(first.Items[1].FirstName);
        Assert.Null(first.Items[1].LastName);
        Assert.Equal("Juan", first.Items[2].FirstName);
        stub.Users[7].FirstName = "Updated";
        stub.Users[7].LastName = "Name";
        var detail = await service.GetDetailsAsync(1);
        Assert.Equal("Updated", detail.FirstName);
        Assert.Equal("Name", detail.LastName);
        Assert.Equal(Role.HeadAdmin, detail.Role);
        var missingDetail = await service.GetDetailsAsync(2);
        Assert.Null(missingDetail.FirstName);
        Assert.Null(missingDetail.LastName);
        var before = stub.NameRequests.Count;
        var systemDetail = await service.GetDetailsAsync(3);
        Assert.Null(systemDetail.FirstName);
        Assert.Null(systemDetail.LastName);
        Assert.Equal(before, stub.NameRequests.Count);
    }

    [Fact]
    public async Task Empty_pages_system_only_pages_and_counts_skip_user_lookups()
    {
        var repository = new MemoryAuditRepository();
        var users = AuditUserRepositoryStub.Create();
        var stub = (AuditUserRepositoryStub)(object)users;
        using var provider = Provider(repository, users);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        await service.GetSummariesAsync(0, new());
        var system = Log(1); system.UserId = null; system.Role = null; repository.Logs.Add(system);
        var page = await service.GetSummariesAsync(0, new());
        Assert.Null(Assert.Single(page.Items).FirstName);
        await service.GetActivityCountsAsync(new());
        Assert.Empty(stub.NameRequests);
        using var db = DatabaseModel();
        var actualUsers = new UserRepository(null!, null!, db);
        Assert.Empty(await actualUsers.GetNamesByIdsAsync([]));
    }

    [Fact]
    public async Task Inclusive_Manila_dates_are_converted_to_exclusive_UTC_boundaries()
    {
        var repository = new MemoryAuditRepository();
        using var provider = Provider(repository);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IAuditLogService>().GetSummariesAsync(0,
            new() { FromDate = new(2026, 10, 10), ToDate = new(2026, 10, 10) });
        Assert.Equal(new DateTime(2026, 10, 9, 16, 0, 0, DateTimeKind.Utc), repository.FromUtc);
        Assert.Equal(new DateTime(2026, 10, 10, 16, 0, 0, DateTimeKind.Utc), repository.ToUtc);
    }

    [Fact]
    public async Task Counts_include_successful_and_failed_security_events_and_share_the_summary_filters()
    {
        var repository = new MemoryAuditRepository();
        var first = Log(1); first.Timestamp = new(2026, 10, 9, 16, 0, 0, DateTimeKind.Utc);
        var login = Log(2); login.Module = Module.Security; login.Action = "VerifyLogin";
        login.Details = "User sign-in completed successfully.";
        login.Timestamp = new(2026, 10, 10, 15, 59, 59, DateTimeKind.Utc);
        var denied = Log(3); denied.Module = Module.Security; denied.Result = LogResult.Failed;
        denied.Action = "AuthorizationFailed"; denied.Details = "Access was denied.";
        denied.Timestamp = new(2026, 10, 10, 16, 0, 0, DateTimeKind.Utc);
        var anonymous = Log(4); anonymous.Module = Module.Security; anonymous.Result = LogResult.Failed;
        anonymous.Action = "AuthorizationFailed"; anonymous.Details = "Anonymous access was denied.";
        anonymous.UserId = null; anonymous.Role = null;
        anonymous.Timestamp = new(2026, 10, 9, 17, 0, 0, DateTimeKind.Utc);
        var older = Log(5); older.Result = LogResult.Failed;
        older.Timestamp = new(2026, 10, 9, 15, 59, 59, DateTimeKind.Utc);
        repository.Logs.AddRange([first, login, denied, anonymous, older]);
        using var provider = Provider(repository);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();

        var all = await service.GetActivityCountsAsync(new());
        Assert.Equal(5, all.RecordedActivities);
        Assert.Equal(2, all.SuccessfulActions);
        Assert.Equal(3, all.SecurityEvents);

        var filters = new AuditLogFilter { FromDate = new(2026, 10, 10), ToDate = new(2026, 10, 10), UserId = 7 };
        var dated = await service.GetActivityCountsAsync(filters);
        Assert.Equal(2, dated.RecordedActivities);
        Assert.Equal(2, dated.SuccessfulActions);
        Assert.Equal(1, dated.SecurityEvents);
        Assert.Equal(new DateTime(2026, 10, 9, 16, 0, 0, DateTimeKind.Utc), repository.FromUtc);
        Assert.Equal(new DateTime(2026, 10, 10, 16, 0, 0, DateTimeKind.Utc), repository.ToUtc);
        Assert.Equal((await service.GetSummariesAsync(0, filters)).Total, dated.RecordedActivities);

        var successes = await service.GetActivityCountsAsync(new()
        {
            UserId = 7, Role = Role.HeadAdmin, Module = Module.Security, Result = LogResult.Success, Search = "sign-in"
        });
        Assert.Equal(1, successes.RecordedActivities);
        Assert.Equal(1, successes.SuccessfulActions);
        Assert.Equal(1, successes.SecurityEvents);
        var empty = await service.GetActivityCountsAsync(new() { UserId = 999 });
        Assert.Equal(0, empty.RecordedActivities);
        Assert.Equal(0, empty.SuccessfulActions);
        Assert.Equal(0, empty.SecurityEvents);
    }

    [Theory]
    [InlineData(AuditLogDateRange.AllTime)]
    [InlineData(AuditLogDateRange.Today)]
    [InlineData(AuditLogDateRange.Yesterday)]
    [InlineData(AuditLogDateRange.Last7Days)]
    [InlineData(AuditLogDateRange.Last30Days)]
    [InlineData(AuditLogDateRange.ThisMonth)]
    [InlineData(AuditLogDateRange.LastMonth)]
    public async Task Date_presets_use_Manila_calendar_days_and_identical_boundaries_for_table_and_counts(AuditLogDateRange range)
    {
        var repository = new MemoryAuditRepository();
        using var provider = Provider(repository);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        var manila = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, manila));
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var filters = new AuditLogFilter { DateRange = range };
        await service.GetSummariesAsync(0, filters);
        (DateOnly? from, DateOnly? to) = range switch
        {
            AuditLogDateRange.AllTime => ((DateOnly?)null, (DateOnly?)null),
            AuditLogDateRange.Today => (today, today.AddDays(1)),
            AuditLogDateRange.Yesterday => (today.AddDays(-1), today),
            AuditLogDateRange.Last7Days => (today.AddDays(-6), today.AddDays(1)),
            AuditLogDateRange.Last30Days => (today.AddDays(-29), today.AddDays(1)),
            AuditLogDateRange.ThisMonth => (monthStart, today.AddDays(1)),
            _ => (monthStart.AddMonths(-1), monthStart)
        };
        DateTime? Expected(DateOnly? date) => date.HasValue
            ? TimeZoneInfo.ConvertTimeToUtc(date.Value.ToDateTime(TimeOnly.MinValue), manila) : null;
        Assert.Equal(Expected(from), repository.FromUtc);
        Assert.Equal(Expected(to), repository.ToUtc);
        await service.GetActivityCountsAsync(filters);
        Assert.Equal(Expected(from), repository.FromUtc);
        Assert.Equal(Expected(to), repository.ToUtc);
        Assert.DoesNotContain("Custom", Enum.GetNames<AuditLogDateRange>());
    }

    [Fact]
    public async Task Multi_select_filters_use_OR_within_selections_and_AND_with_search_and_pagination()
    {
        var repository = new MemoryAuditRepository();
        foreach (var id in Enumerable.Range(1, 24))
        {
            var log = Log(id);
            log.Module = id % 2 == 0 ? Module.Users : Module.Security;
            log.Result = id % 3 == 0 ? LogResult.Failed : LogResult.Success;
            repository.Logs.Add(log);
        }
        using var provider = Provider(repository);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        var filters = new AuditLogFilter
        {
            Modules = [Module.Users, Module.Security], Results = [LogResult.Success], Search = "profile"
        };
        var first = await service.GetSummariesAsync(0, filters);
        var next = await service.GetSummariesAsync(10, filters);
        Assert.Equal(16, first.Total);
        Assert.Equal(10, first.Items.Count);
        Assert.True(first.HasMore);
        Assert.Equal(6, next.Items.Count);
        Assert.False(next.HasMore);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(next.Items.Select(x => x.Id)));
        Assert.All(first.Items.Concat(next.Items), x => Assert.Equal(LogResult.Success, x.Result));
        var counts = await service.GetActivityCountsAsync(filters);
        Assert.Equal(16, counts.RecordedActivities);
        Assert.Equal(16, counts.SuccessfulActions);
        Assert.Equal(8, counts.SecurityEvents);
        filters.Modules = [Module.Users, Module.Users]; filters.Results = [LogResult.Success, LogResult.Failed];
        var bothResults = await service.GetSummariesAsync(0, filters);
        Assert.Equal(12, bothResults.Total);
        Assert.Contains(bothResults.Items, x => x.Result == LogResult.Failed);
        Assert.Single(filters.Modules);
    }

    [Fact]
    public async Task Invalid_multi_select_and_preset_combinations_are_rejected_for_table_and_counts()
    {
        using var provider = Provider(new());
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        foreach (var filters in new AuditLogFilter[]
        {
            new() { Modules = [(Module)999] }, new() { Results = [(LogResult)999] },
            new() { Modules = Enumerable.Repeat(Module.Users, 11).ToArray() },
            new() { Results = [LogResult.Success, LogResult.Failed, LogResult.Success] },
            new() { Module = Module.Users, Modules = [Module.Users] },
            new() { Result = LogResult.Success, Results = [LogResult.Failed] },
            new() { DateRange = (AuditLogDateRange)999 },
            new() { DateRange = AuditLogDateRange.Last7Days, FromDate = new(2026, 10, 10) },
            new() { DateRange = AuditLogDateRange.AllTime, ToDate = new(2026, 10, 10) }
        })
        {
            await Assert.ThrowsAsync<ValidationException>(() => service.GetSummariesAsync(0, filters));
            await Assert.ThrowsAsync<ValidationException>(() => service.GetActivityCountsAsync(filters));
        }
    }

    [Fact]
    public void PostgreSQL_translates_multi_select_enums_to_their_stored_string_values_without_database_access()
    {
        using var db = DatabaseModel();
        var repository = new AuditLogRepository(db);
        var filters = new AuditLogFilter
        {
            Modules = [Module.Users, Module.Security], Results = [LogResult.Success, LogResult.Failed], Search = "profile"
        };
        var query = (IQueryable<AuditLog>)typeof(AuditLogRepository)
            .GetMethod("Filter", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(repository, [filters, null, null])!;
        var sql = query.ToQueryString();
        Assert.Contains("ANY", sql);
        Assert.Contains("Users", sql);
        Assert.Contains("Security", sql);
        Assert.Contains("Success", sql);
        Assert.Contains("Failed", sql);
    }

    [Fact]
    public async Task Filters_are_combined_and_search_includes_action_description_and_target()
    {
        var repository = new MemoryAuditRepository();
        var selected = Log(1); selected.Result = LogResult.Failed; selected.Details = "Profile operation failed.";
        var other = Log(2); other.UserId = 8;
        repository.Logs.AddRange([selected, other]);
        using var provider = Provider(repository);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        var page = await service.GetSummariesAsync(0, new()
        {
            UserId = 7, Role = Role.HeadAdmin, Module = Module.Users, Result = LogResult.Failed, Search = " OPERATION "
        });
        Assert.Equal(1, Assert.Single(page.Items).Id);
        Assert.Equal(1, page.Total);
        Assert.Single((await service.GetSummariesAsync(0, new() { Search = "failed" })).Items);
        Assert.Equal(2, (await service.GetSummariesAsync(0, new() { Search = "7" })).Items.Count);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 999)]
    public async Task Invalid_offset_and_enum_are_rejected(int offset, int module)
    {
        using var provider = Provider(new());
        using var scope = provider.CreateScope();
        await Assert.ThrowsAsync<ValidationException>(() => scope.ServiceProvider.GetRequiredService<IAuditLogService>()
            .GetSummariesAsync(offset, new() { Module = (Module)module }));
    }

    [Fact]
    public async Task Invalid_dates_search_actor_role_and_result_are_rejected()
    {
        using var provider = Provider(new());
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        foreach (var filter in new AuditLogFilter[]
        {
            new() { FromDate = new(2026, 10, 11), ToDate = new(2026, 10, 10) },
            new() { FromDate = DateOnly.MinValue }, new() { ToDate = DateOnly.MaxValue },
            new() { Search = new string('a', 201) }, new() { UserId = 0 },
            new() { Role = (Role)999 }, new() { Result = (LogResult)999 }
        })
        {
            await Assert.ThrowsAsync<ValidationException>(() => service.GetSummariesAsync(0, filter));
            await Assert.ThrowsAsync<ValidationException>(() => service.GetActivityCountsAsync(filter));
        }
    }

    [Fact]
    public async Task Detail_validates_ID_and_returns_a_historical_role_and_full_description()
    {
        var repository = new MemoryAuditRepository(); repository.Logs.Add(Log(1));
        using var provider = Provider(repository);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        var detail = await service.GetDetailsAsync(1);
        Assert.Equal("7", detail.TargetId);
        Assert.Equal("User updated a profile.", detail.Details);
        Assert.Equal(Role.HeadAdmin, detail.Role);
        await Assert.ThrowsAsync<ValidationException>(() => service.GetDetailsAsync(0));
        await Assert.ThrowsAsync<RecordNotFoundException>(() => service.GetDetailsAsync(2));
    }

    [Fact]
    public async Task System_record_is_copied_with_server_ID_and_UTC_time_in_an_independent_scope()
    {
        var repository = new MemoryAuditRepository();
        using var provider = Provider(repository);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        var input = Log(99); input.UserId = null; input.Role = null;
        var before = DateTime.UtcNow;
        await service.RecordAsync(input);
        var stored = Assert.Single(repository.Logs);
        Assert.NotSame(input, stored);
        Assert.Equal(99, input.Id);
        Assert.Equal(1, stored.Id);
        Assert.Null(stored.UserId);
        Assert.Null(stored.Role);
        Assert.InRange(stored.Timestamp, before, DateTime.UtcNow);
        Assert.Equal(DateTimeKind.Utc, stored.Timestamp.Kind);
    }

    [Fact]
    public async Task Invalid_log_and_failed_audit_write_do_not_escape_into_the_business_operation()
    {
        var repository = new MemoryAuditRepository();
        using var provider = Provider(repository);
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        var invalid = Log(); invalid.Action = new string('a', 101);
        await service.RecordAsync(invalid);
        Assert.Empty(repository.Logs);
        repository.FailWrites = true;
        await service.RecordAsync(Log());
        await service.AddInTransactionAsync(Log());
        Assert.Empty(repository.Logs);
    }

    [Fact]
    public async Task Transaction_commits_a_staged_success_once_and_rollback_allows_a_separate_failed_record()
    {
        var repository = new MemoryAuditRepository();
        using var provider = Provider(repository);
        using var scope = provider.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<AuditLogContext>();
        var service = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
        using var db = DatabaseModel();
        using var unit = new UnitOfWork(db, audit);
        audit.Entry = Log(); audit.SaveWithTransaction = true;
        var transaction = new FakeTransaction(repository);
        typeof(UnitOfWork).GetField("_currentTransaction", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(unit, transaction);
        await scope.ServiceProvider.GetRequiredService<IAuditLogService>().StageCurrentAsync();
        await unit.CommitAsync();
        Assert.True(transaction.Committed);
        Assert.True(audit.Stored);
        Assert.Single(repository.Logs);

        audit.Entry = Log(); audit.Stored = false; audit.Staged = false;
        var failedTransaction = new FakeTransaction(repository) { FailCommit = true };
        typeof(UnitOfWork).GetField("_currentTransaction", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(unit, failedTransaction);
        await service.StageCurrentAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => unit.CommitAsync());
        Assert.True(failedTransaction.RolledBack);
        Assert.False(audit.Stored);
        Assert.False(audit.Staged);
        Assert.Single(repository.Logs);
        audit.Entry.Result = LogResult.Failed;
        await service.RecordAsync(audit.Entry);
        Assert.Equal(2, repository.Logs.Count);
        Assert.Equal(LogResult.Failed, repository.Logs[1].Result);
    }

    [Fact]
    public async Task Failed_transactional_audit_does_not_prevent_a_business_commit()
    {
        var repository = new MemoryAuditRepository { FailWrites = true };
        using var provider = Provider(repository);
        using var scope = provider.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<AuditLogContext>();
        audit.Entry = Log(); audit.SaveWithTransaction = true;
        using var db = DatabaseModel();
        using var unit = new UnitOfWork(db, audit);
        var transaction = new FakeTransaction(repository);
        typeof(UnitOfWork).GetField("_currentTransaction", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(unit, transaction);
        await scope.ServiceProvider.GetRequiredService<IAuditLogService>().StageCurrentAsync();
        await unit.CommitAsync();
        Assert.True(transaction.Committed);
        Assert.False(audit.Stored);
    }

    [Fact]
    public void Migration_model_and_PostgreSQL_SQL_define_only_the_new_audit_table_and_indexes()
    {
        using var db = DatabaseModel();
        var model = db.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(AuditLog))!;
        Assert.Equal("audit_logs", entity.GetTableName());
        Assert.True(entity.FindProperty(nameof(AuditLog.UserId))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(AuditLog.Role))!.IsNullable);
        Assert.Equal(500, entity.FindProperty(nameof(AuditLog.Details))!.GetMaxLength());
        Assert.Empty(entity.GetForeignKeys());
        Assert.Equal(4, entity.GetIndexes().Count());
        var migration = new AddAuditLogs();
        Assert.Equal("20261010000000_AddAuditLogs", typeof(AddAuditLogs).GetCustomAttribute<MigrationAttribute>()!.Id);
        Assert.Equal("audit_logs", Assert.Single(migration.UpOperations.OfType<CreateTableOperation>()).Name);
        Assert.Equal(4, migration.UpOperations.OfType<CreateIndexOperation>().Count());
        Assert.Equal(5, migration.UpOperations.Count);
        var sql = string.Join('\n', db.GetService<IMigrationsSqlGenerator>().Generate(migration.UpOperations, model).Select(x => x.CommandText));
        Assert.Contains("audit_logs", sql);
        Assert.Contains("timestamp with time zone", sql);
        Assert.Contains("CURRENT_TIMESTAMP", sql);
        Assert.DoesNotContain("FOREIGN KEY", sql);
        var target = db.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel, designTime: true);
        var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(target.GetRelationalModel(), model.GetRelationalModel());
        Assert.DoesNotContain(differences, op => op.GetType().GetProperty("Table")?.GetValue(op) is "audit_logs");
        Assert.Empty(differences);
    }

    private static AppDbContext DatabaseModel() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=127.0.0.1;Database=model_only;Username=model_only").UseSnakeCaseNamingConvention().Options);
}

internal sealed class FakeTransaction : IDbContextTransaction
{
    private readonly MemoryAuditRepository _repository;
    private readonly int _startingCount;
    public FakeTransaction(MemoryAuditRepository repository) { _repository = repository; _startingCount = repository.Logs.Count; }
    public Guid TransactionId { get; } = Guid.NewGuid();
    public bool Committed { get; private set; }
    public bool RolledBack { get; private set; }
    public bool FailCommit { get; set; }
    public void Commit() { if (FailCommit) throw new InvalidOperationException("Commit failed."); Committed = true; }
    public Task CommitAsync(CancellationToken cancellationToken = default) { Commit(); return Task.CompletedTask; }
    public void Rollback() { RolledBack = true; _repository.Logs.RemoveRange(_startingCount, _repository.Logs.Count - _startingCount); }
    public Task RollbackAsync(CancellationToken cancellationToken = default) { Rollback(); return Task.CompletedTask; }
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
