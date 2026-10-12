using System.Data.Common;
using System.Net;
using System.Reflection;
using System.Text.Json;
using MarikinaMarket.API.Application;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using MarikinaMarket.API.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MarikinaMarket.Tests;

public class AdminTicketDetailTests
{
    [Fact]
    public async Task Detail_signs_all_proofs_in_one_batch_and_preserves_all_service_logs()
    {
        var fixture = new Fixture();
        fixture.Ticket.TicketEvidences = ["evidence.png"];
        fixture.Ticket.ProofUrls = ["receipt.png", "receipt-2.png"];
        fixture.Ticket.CommunityServiceHours = 8;
        fixture.Ticket.HoursCompleted = 5.5m;
        fixture.Ticket.CommunityServiceLogs =
        [
            new() { ServiceDate = DateTime.UnixEpoch, HoursWorked = 2m, ProofUrl = "service.png" },
            new() { ServiceDate = DateTime.UnixEpoch.AddDays(1), HoursWorked = 3.5m, ProofUrl = "receipt.png" }
        ];

        var result = await fixture.Service.GetAdminTicketDetailAsync(42);

        Assert.Equal(1, fixture.StorageCalls);
        Assert.Equal(["evidence.png", "receipt.png", "receipt-2.png", "service.png"], fixture.SignedKeys);
        Assert.Equal([Signed("receipt.png"), Signed("receipt-2.png")], result.ProofUrls);
        Assert.Equal(result.ProofUrls, result.TicketReceipts);
        Assert.Equal([Signed("evidence.png")], result.TicketEvidences);
        Assert.Equal(8, result.CommunityServiceHours);
        Assert.Equal(5.5m, result.HoursCompleted);
        Assert.Collection(result.CommunityServiceLogs,
            first => { Assert.Equal(2m, first.HoursWorked); Assert.Equal(Signed("service.png"), first.ProofUrl); },
            second => { Assert.Equal(3.5m, second.HoursWorked); Assert.Equal(Signed("receipt.png"), second.ProofUrl); });
    }

    [Fact]
    public async Task Empty_detail_returns_empty_arrays_and_zero_hours_without_calling_storage()
    {
        var fixture = new Fixture();
        fixture.Ticket.TicketEvidences = null;
        var result = await fixture.Service.GetAdminTicketDetailAsync(42);
        Assert.Empty(result.ProofUrls);
        Assert.Empty(result.TicketReceipts);
        Assert.Empty(result.TicketEvidences!);
        Assert.Empty(result.CommunityServiceLogs);
        Assert.Null(result.CommunityServiceHours);
        Assert.Equal(0m, result.HoursCompleted);
        Assert.Equal(0, fixture.StorageCalls);
    }

    [Fact]
    public async Task Blank_proof_keys_do_not_cause_storage_lookup_failures_or_drop_logs()
    {
        var fixture = new Fixture();
        fixture.Ticket.TicketEvidences = [" "];
        fixture.Ticket.ProofUrls = [""];
        fixture.Ticket.CommunityServiceLogs = [new() { HoursWorked = 1, ProofUrl = " " }];
        var result = await fixture.Service.GetAdminTicketDetailAsync(42);
        Assert.Empty(result.ProofUrls);
        Assert.Equal("", Assert.Single(result.CommunityServiceLogs).ProofUrl);
        Assert.Equal(0, fixture.StorageCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Invalid_ids_are_rejected_before_repository_access(int id)
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<InvalidRequestException>(() => fixture.Service.GetAdminTicketDetailAsync(id));
        Assert.Equal(0, fixture.RepositoryCalls);
        Assert.Equal(0, fixture.StorageCalls);
    }

    [Fact]
    public async Task Missing_ticket_is_not_found_without_calling_storage()
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<RecordNotFoundException>(() => fixture.Service.GetAdminTicketDetailAsync(99));
        Assert.Equal(0, fixture.StorageCalls);
    }

    [Fact]
    public async Task Detail_endpoint_serializes_new_fields_and_legacy_receipts()
    {
        var fixture = new Fixture();
        fixture.Ticket.ProofUrls = ["receipt.png"];
        fixture.Ticket.CommunityServiceHours = 8;
        fixture.Ticket.HoursCompleted = 2.5m;
        fixture.Ticket.CommunityServiceLogs =
            [new() { ServiceDate = DateTime.UnixEpoch, HoursWorked = 2.5m, ProofUrl = "service.png" }];
        await using var host = await AuditHost.CreateAsync(services => services.AddSingleton<ITicketService>(fixture.Service));
        var response = await host.SendAsync(HttpMethod.Get, "/api/admin/tickets/42");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var detail = json.RootElement;
        Assert.Equal(Signed("receipt.png"), detail.GetProperty("proof_urls")[0].GetString());
        Assert.Equal(detail.GetProperty("proof_urls").GetRawText(), detail.GetProperty("ticket_receipts").GetRawText());
        Assert.Equal(8, detail.GetProperty("community_service_hours").GetInt32());
        Assert.Equal(2.5m, detail.GetProperty("hours_completed").GetDecimal());
        var log = detail.GetProperty("community_service_logs")[0];
        Assert.Equal(2.5m, log.GetProperty("hours_worked").GetDecimal());
        Assert.Equal(Signed("service.png"), log.GetProperty("proof_url").GetString());
        Assert.Equal(DateTime.UnixEpoch, log.GetProperty("service_date").GetDateTime());
    }

    [Theory]
    [InlineData("HeadAdmin", 0, HttpStatusCode.BadRequest)]
    [InlineData("HeadAdmin", 99, HttpStatusCode.NotFound)]
    [InlineData(null, 42, HttpStatusCode.Unauthorized)]
    [InlineData("MarketEnforcer", 42, HttpStatusCode.Forbidden)]
    public async Task Detail_endpoint_returns_expected_errors(string? role, int id, HttpStatusCode expected)
    {
        var fixture = new Fixture();
        await using var host = await AuditHost.CreateAsync(services => services.AddSingleton<ITicketService>(fixture.Service));
        var response = await host.SendAsync(HttpMethod.Get, $"/api/admin/tickets/{id}", role);
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(0, fixture.StorageCalls);
    }

    [Fact]
    public async Task Repository_translates_logs_and_hour_sum_without_opening_a_database()
    {
        var capture = new CaptureCommand();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused;Database=model_only;Username=model_only")
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new SuppressConnection(), capture).Options;
        await using var context = new AppDbContext(options);
        var repository = new TicketRepository(context);

        await Assert.ThrowsAsync<CapturedCommandException>(() => repository.GetAdminTicketDetailAsync(42));
        Assert.NotNull(capture.Sql);
        Assert.Contains("community_service_logs", capture.Sql);
        Assert.Contains("sum(", capture.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hours_worked", capture.Sql);
        Assert.Contains("service_date", capture.Sql);
        Assert.Contains("ORDER BY", capture.Sql);
        Assert.Contains(42, capture.Parameters);
    }

    private static string Signed(string key) => $"https://storage.example.com/{key}?signed=1";

    private sealed class Fixture
    {
        public AdminTicketDetailResponse Ticket { get; } = new()
        {
            TicketId = 42, BusinessId = "BUS-42", BusinessName = "Store", FirstName = "Juan", LastName = "Santos",
            Address = "", MarketSectionName = "Dry Goods", Description = "Violation", EnforcerFirstName = "Maria",
            EnforcerLastName = "Reyes", Version = 123
        };
        public TicketService Service { get; }
        public int RepositoryCalls { get; private set; }
        public int StorageCalls { get; private set; }
        public List<string> SignedKeys { get; private set; } = [];

        public Fixture()
        {
            var repository = Proxy<ITicketRepository>((method, args) =>
            {
                Assert.Equal(nameof(ITicketRepository.GetAdminTicketDetailAsync), method.Name);
                RepositoryCalls++;
                return Task.FromResult<AdminTicketDetailResponse?>((int)args![0]! == 42 ? Ticket : null);
            });
            var storage = Proxy<IStorageService>((method, args) =>
            {
                Assert.Equal(nameof(IStorageService.GetPresignedUrlsAsync), method.Name);
                Assert.Equal(B2BucketType.Evidence, args![0]);
                StorageCalls++;
                SignedKeys = ((IEnumerable<string>)args[1]!).ToList();
                return Task.FromResult(SignedKeys.ToDictionary(key => key, Signed));
            });
            Service = new TicketService(repository, null!, null!, null!, storage, null!, null!);
        }
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, DetailProxy>();
        ((DetailProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class DetailProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args);
    }

    private sealed class SuppressConnection : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(InterceptionResult.Suppress());
    }

    private sealed class CapturedCommandException : Exception { }

    private sealed class CaptureCommand : DbCommandInterceptor
    {
        public string? Sql { get; private set; }
        public List<object> Parameters { get; private set; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Sql = command.CommandText;
            Parameters = command.Parameters.Cast<DbParameter>().Select(parameter => parameter.Value!).ToList();
            throw new CapturedCommandException();
        }
    }
}
