using System.ComponentModel.DataAnnotations;
using System.Reflection;
using MarikinaMarket.API.Application;
using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Application.DTOs.Vendor.Request;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json;
using MarikinaMarket.API.Infrastructure.Persistence;
using MarikinaMarket.API.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace MarikinaMarket.Tests;

public class RegistrationVersionTests
{
    [Fact]
    public async Task RepositoryProjectsTheDatabaseConcurrencyToken()
    {
        var fixture = new ReviewFixture();
        var records = new VendorTicketDbSet<VendorRegistrationRequest>([fixture.Record]);
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
        var databaseRoot = ((IQueryable<VendorRegistrationRequest>)context.VendorRegistrationRequests).Expression;
        context.VendorRegistrationRequests = records;
        var summaries = await new VendorRepository(context).GetVendorRegistrationSummariesAsync(0, 10, new VendorRegistrationRequestFilters());
        Assert.Equal((uint)1200, Assert.Single(summaries).Version);
        var expression = new ReplaceReportQueryRoot(records.RootExpression, databaseRoot).Visit(records.State.LastExpression!);
        var query = context.Set<VendorRegistrationRequest>().AsQueryable().Provider.CreateQuery<VendorRegistrationSummary>(expression!);
        Assert.Contains("xmin", query.ToQueryString());
        var version = context.Model.FindEntityType(typeof(VendorRegistrationRequest))!.FindProperty("Version")!;
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
    }

    [Theory]
    [InlineData("approve-registry")]
    [InlineData("decline-registry")]
    [InlineData("request-more-information")]
    public async Task HttpVersionValidationAndConflict(string action)
    {
        var fixture = new ReviewFixture();
        await using var host = await AuditHost.CreateAsync(services => services.AddSingleton<IVendorService>(fixture.Service));
        var route = "/api/vendor/" + action;
        foreach (var version in new[] { "", ",\"version\":0", ",\"version\":null", ",\"version\":-1", ",\"version\":4294967296" })
        {
            using var invalid = await host.SendAsync(HttpMethod.Post, route, body:
                "{\"vendor_registration_id\":42,\"review_reason\":\"Documents\",\"review_remarks\":\"Provide clearer documents.\"" + version + "}");
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
        using var stale = await host.SendAsync(HttpMethod.Post, route, body:
            "{\"vendor_registration_id\":42,\"version\":1199,\"review_reason\":\"Documents\",\"review_remarks\":\"Provide clearer documents.\"}");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var conflict = JsonDocument.Parse(await stale.Content.ReadAsStringAsync());
        Assert.Contains("refresh", conflict.RootElement.GetProperty("message").GetString()!, StringComparison.OrdinalIgnoreCase);
        using var success = await host.SendAsync(HttpMethod.Post, route, body:
            "{\"vendor_registration_id\":42,\"version\":1200,\"admin_id\":999,\"review_reason\":\"Documents\",\"review_remarks\":\"Provide clearer documents.\"}");
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        using var json = JsonDocument.Parse(await success.Content.ReadAsStringAsync());
        Assert.Equal(1300, json.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(7, json.RootElement.GetProperty("admin_id").GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DatabaseSerializationFailureIsAConcurrencyConflict(bool wrapped)
    {
        var postgres = new PostgresException("Concurrent write", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure);
        Exception error = wrapped ? new DbUpdateException("Save failed", postgres) : postgres;
        using var context = new FailingReviewContext(error);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => new UnitOfWork(context).SaveChangesAsync());
    }

    [Fact]
    public async Task CommitSerializationFailureRollsBackAndDisposesTransaction()
    {
        using var context = new FailingReviewContext(new InvalidOperationException());
        var transaction = new FailingReviewTransaction();
        var unit = new UnitOfWork(context);
        typeof(UnitOfWork).GetField("_currentTransaction", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(unit, transaction);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => unit.CommitAsync());
        Assert.True(transaction.RolledBack);
        Assert.True(transaction.Disposed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task IdentityWritesTranslateSerializationFailures(bool wrapped, bool assignRole)
    {
        var postgres = new PostgresException("Concurrent write", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure);
        Exception error = wrapped ? new DbUpdateException("Save failed", postgres) : postgres;
        using var manager = new FailingReviewUserManager(error);
        var repository = new UserRepository(manager, null!, null!);
        var user = new User { FirstName = "Juan", LastName = "Reyes", DateOfBirth = new DateOnly(1990, 1, 1),
            HouseNumber = "12", Street = "Sampaguita", Barangay = "Santa Elena", City = "Marikina" };
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => assignRole
            ? repository.AddToRoleAsync(user, "MarketVendor") : repository.CreateUserWithPassAsync(user));
    }

    [Fact]
    public async Task ReadEndpointsExposeSameVersionWithoutSaving()
    {
        var fixture = new ReviewFixture();
        await using var host = await AuditHost.CreateAsync(services =>
        {
            services.AddSingleton<IVendorService>(fixture.Service);
            services.AddSingleton(ReviewBoundaryProxy.Create<ITicketService>((m, a) => throw new InvalidOperationException(m.Name)));
        });
        using var list = await host.SendAsync(HttpMethod.Get, "/api/admin/vendors/registration-requests?offset=0");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var page = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Equal(1200, page.RootElement.GetProperty("items")[0].GetProperty("version").GetInt32());
        using var detail = await host.SendAsync(HttpMethod.Get, "/api/admin/vendors/registration-requests/42");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var record = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.Equal(1200, record.RootElement.GetProperty("version").GetInt32());
        Assert.Equal(0, fixture.Saves);
    }
    [Theory]
    [InlineData(typeof(RegistrationApprovalRequest))]
    [InlineData(typeof(RegistrationDeclineRequest))]
    [InlineData(typeof(RegistrationInformationRequest))]
    public void ZeroVersionIsInvalid(Type type)
    {
        var request = Activator.CreateInstance(type)!;
        var context = new ValidationContext(request) { MemberName = "Version" };
        Assert.False(Validator.TryValidateProperty((uint)0, context, []));
        Assert.True(Validator.TryValidateProperty(uint.MaxValue, context, []));
    }

    [Theory]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected)]
    [InlineData(RequestStatus.NeedsInformation)]
    public async Task StaleReviewFailsBeforeAccountCreationOrMutation(RequestStatus status)
    {
        var fixture = new ReviewFixture();
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => fixture.Review(status, 1199));
        Assert.Equal(RequestStatus.Pending, fixture.Record.Status);
        Assert.Equal(0, fixture.UserCreates);
        Assert.Equal(0, fixture.Saves);
        Assert.Equal(0, fixture.Emails);
        Assert.Equal(1, fixture.Rollbacks);
    }

    [Theory]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected)]
    [InlineData(RequestStatus.NeedsInformation)]
    public async Task SuccessfulReviewReturnsDatabaseVersionAndBlocksSecondReview(RequestStatus status)
    {
        var fixture = new ReviewFixture();
        var result = await fixture.Review(status, 1200);
        Assert.Equal((uint)1300, result.GetType().GetProperty("Version")!.GetValue(result));
        Assert.Equal((uint)1200, fixture.OriginalVersion);
        Assert.Equal(status, fixture.Record.Status);
        Assert.Equal(1, fixture.Commits);
        Assert.Equal(1, fixture.Emails);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => fixture.Review(status, 1200));
        Assert.Equal(1, fixture.Saves);
    }

    [Theory]
    [InlineData(RequestStatus.Approved)]
    [InlineData(RequestStatus.Rejected)]
    [InlineData(RequestStatus.NeedsInformation)]
    public async Task SaveConflictRollsBackAndSendsNoEmail(RequestStatus status)
    {
        var fixture = new ReviewFixture { FailSave = true };
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => fixture.Review(status, 1200));
        Assert.Equal(RequestStatus.Pending, fixture.Record.Status);
        Assert.Equal((uint)1200, fixture.Record.Version);
        Assert.Equal(1, fixture.Rollbacks);
        Assert.Equal(0, fixture.Commits);
        Assert.Equal(0, fixture.Emails);
    }

    [Fact]
    public async Task DetailExposesVersionWithoutWriting()
    {
        var fixture = new ReviewFixture();
        var result = await fixture.Service.GetVendorRegistrationDetailsAsync(42);
        Assert.NotNull(result.GetType().GetProperty("Version"));
        Assert.Equal((uint)1200, result.GetType().GetProperty("Version")!.GetValue(result));
        Assert.Equal(0, fixture.Saves);
    }
}

internal sealed class FailingReviewContext(Exception error)
    : AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options)
{
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.FromException<int>(error);
}

internal sealed class FailingReviewTransaction : IDbContextTransaction
{
    public Guid TransactionId => Guid.Empty;
    public bool RolledBack, Disposed;
    public void Commit() => throw new NotSupportedException();
    public Task CommitAsync(CancellationToken cancellationToken = default)
        => Task.FromException(new PostgresException("Concurrent write", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure));
    public void Rollback() => RolledBack = true;
    public Task RollbackAsync(CancellationToken cancellationToken = default) { Rollback(); return Task.CompletedTask; }
    public void Dispose() => Disposed = true;
    public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
}

internal sealed class FailingReviewUserManager(Exception error) : UserManager<User>(
    ReviewBoundaryProxy.Create<IUserStore<User>>((m, a) => null), Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
    new PasswordHasher<User>(), [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
    null!, NullLogger<UserManager<User>>.Instance)
{
    public override Task<IdentityResult> CreateAsync(User user) => Task.FromException<IdentityResult>(error);
    public override Task<IdentityResult> AddToRoleAsync(User user, string role) => Task.FromException<IdentityResult>(error);
}

public class ReviewBoundaryProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? []);
    public static T Create<T>(Func<MethodInfo, object?[], object?> handler) where T : class
    {
        var proxy = Create<T, ReviewBoundaryProxy>();
        ((ReviewBoundaryProxy)(object)proxy).Handler = handler;
        return proxy;
    }
}

internal sealed class ReviewFixture
{
    public VendorRegistrationRequest Record { get; } = new()
    {
        Id = 42, Version = 1200, Status = RequestStatus.Pending,
        GovernmentIdNumber = "SAMPLE-ID", GovernmentIdPhotoUrl = "", BusinessDocumentPhotoUrl = "",
        BusinessId = "MM-2026-0042", BusinessName = "Reyes Fresh Fish", NatureOfBusiness = "Fish retail",
        FirstName = "Juan", LastName = "Reyes", HouseNumber = "12", Street = "Sampaguita",
        Barangay = "Santa Elena", City = "Marikina", PhoneNumber = "09171234567",
        Email = "juan@example.invalid", Password = "sample-hash", MarketSectionId = 1,
        MarketSection = new MarketSection { Id = 1, Name = "Fish and Seafood", Description = "Fish retail section" }
    };
    public VendorService Service { get; }
    public int UserCreates, Saves, Emails, Rollbacks, Commits;
    public uint OriginalVersion;
    public bool FailSave;
    public ReviewFixture()
    {
        var vendors = ReviewBoundaryProxy.Create<IVendorRepository>((m, a) => m.Name switch
        {
            "GetRegistrationById" => Task.FromResult<VendorRegistrationRequest?>(Record),
            "GetVendorRegistrationSummariesAsync" => Task.FromResult(new List<VendorRegistrationSummary>
            {
                new() { RegistrationId = Record.Id, BusinessId = Record.BusinessId, BusinessName = Record.BusinessName,
                    VendorName = "Juan Reyes", MarketSectionName = "Fish and Seafood", Version = Record.Version }
            }),
            "GetVendorRegistrationSummaryCountAsync" => Task.FromResult(1),
            "SetOriginalVersion" => SetVersion((uint)a[1]!),
            "CreateVendorAsync" => Task.FromResult((VendorProfile)a[0]!),
            _ => throw new InvalidOperationException(m.Name)
        });
        var users = ReviewBoundaryProxy.Create<IUserRepository>((m, a) => m.Name switch
        {
            "GetNextUserNameAsync" => Task.FromResult("VEN0001-2026"),
            "CreateUserWithPassAsync" => CreateUser(),
            "AddToRoleAsync" => Task.FromResult(IdentityResult.Success),
            _ => throw new InvalidOperationException(m.Name)
        });
        var unit = ReviewBoundaryProxy.Create<IUnitOfWork>((m, a) => m.Name switch
        {
            "BeginTransactionAsync" => Task.CompletedTask,
            "SaveChangesAsync" => Save(),
            "CommitAsync" => Commit(),
            "RollbackAsync" => Rollback(),
            _ => throw new InvalidOperationException(m.Name)
        });
        Service = new VendorService(users, vendors, unit,
            ReviewBoundaryProxy.Create<IMarketSectionRepository>((m, a) => Task.FromResult(Record.MarketSection)),
            null!, null!, ReviewBoundaryProxy.Create<IEmailService>((m, a) => Email()), null!,
            NullLogger<VendorService>.Instance);
    }
    private object? SetVersion(uint version) { OriginalVersion = version; return null; }
    private Task<IdentityResult> CreateUser() { UserCreates++; return Task.FromResult(IdentityResult.Success); }
    private Task<int> Save()
    {
        Saves++;
        if (FailSave) throw new ConcurrencyConflictException("Concurrent update");
        Record.Version = 1300; // Simulate the database-generated xmin at the repository boundary.
        return Task.FromResult(1);
    }
    private Task Commit() { Commits++; return Task.CompletedTask; }
    private Task Rollback()
    {
        Rollbacks++;
        if (Commits == 0) { Record.Status = RequestStatus.Pending; Record.Version = 1200; }
        return Task.CompletedTask;
    }
    private Task Email() { Emails++; return Task.CompletedTask; }
    public async Task<object> Review(RequestStatus status, uint version)
    {
        var request = new RegistrationAdminAction
        {
            VendorRegistrationId = 42, AdminId = 7, Version = version, RequestStatus = status,
            ReviewReason = "Document verification", ReviewRemarks = "Please submit a clear document."
        };
        return status switch
        {
            RequestStatus.Approved => await Service.ApproveVendorRegistration(42, request),
            RequestStatus.Rejected => await Service.DeclineVendorRegistration(42, request),
            _ => await Service.RequestMoreInformation(42, request)
        };
    }
}
