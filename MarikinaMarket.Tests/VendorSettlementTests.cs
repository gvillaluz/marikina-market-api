using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using MarikinaMarket.API.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MarikinaMarket.Tests;

public class VendorSettlementTests
{
    private const string Receipt = "/api/vendor/tickets/101/settlement/receipt";
    private const string Community = "/api/vendor/tickets/101/settlement/community-service";

    [Theory]
    [InlineData(PenaltyType.CashFine)]
    [InlineData(PenaltyType.BloodDonation)]
    public async Task ReceiptGet_ReturnsTheSameSignedProofsAsEnforcer(PenaltyType penalty)
    {
        await using var host = await VendorTicketHost.CreateAsync(configureTickets: tickets =>
        {
            tickets[0].PenaltyType = penalty;
            tickets[0].ProofUrls = ["receipts/first.png", "receipts/second.jpg"];
        });
        using var vendor = await host.GetAsync(Receipt);
        Assert.Equal(HttpStatusCode.OK, vendor.StatusCode);
        var body = await vendor.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(101, json.RootElement.GetProperty("ticket_id").GetInt32());
        Assert.Equal(penalty.ToString(), json.RootElement.GetProperty("penalty_type").GetString());
        Assert.Equal(new[] { VendorTicketStorageBoundary.Signed("receipts/first.png"), VendorTicketStorageBoundary.Signed("receipts/second.jpg") },
            json.RootElement.GetProperty("proof_urls").EnumerateArray().Select(value => value.GetString()));
        using var enforcer = await host.GetAsync("/api/enforcer/tickets/101/settlement/receipt", role: "MarketEnforcer", userId: "12");
        Assert.Equal(HttpStatusCode.OK, enforcer.StatusCode);
        Assert.Equal(body, await enforcer.Content.ReadAsStringAsync());
        Assert.Equal(0, host.UnitOfWork.SaveCalls);
    }

    [Theory]
    [InlineData("proof.jpg", "image/jpeg", PenaltyType.CashFine)]
    [InlineData("proof.jpeg", "image/jpeg", PenaltyType.BloodDonation)]
    [InlineData("proof.png", "image/png", PenaltyType.CashFine)]
    public async Task ReceiptPost_AppendsProofAndUpdatesUtcTimestampWithoutChangingStatus(
        string filename, string mime, PenaltyType penalty)
    {
        await using var host = await VendorTicketHost.CreateAsync(configureTickets: tickets =>
        {
            tickets[0].PenaltyType = penalty;
            tickets[0].ProofUrls = ["receipts/first.png"];
        });
        var before = DateTime.UtcNow;
        using var form = Proof(filename, mime);
        form.Add(new StringContent("88"), "user_id");
        form.Add(new StringContent("7"), "vendor_id");
        using var response = await host.SendAsync(HttpMethod.Post, Receipt, form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ticket = host.Tickets[0];
        var key = Assert.Single(host.Storage.UploadedKeys);
        Assert.Matches(@"^tickets/[0-9]{4}/[0-9]{2}/[0-9a-f-]{36}\.(jpg|jpeg|png)$", key);
        Assert.Equal(new[] { "receipts/first.png", key }, ticket.ProofUrls);
        Assert.Equal(TicketStatus.Pending, ticket.Status);
        Assert.Null(ticket.ResolvedAt);
        Assert.Equal(DateTimeKind.Utc, ticket.UpdatedAt.Kind);
        Assert.InRange(ticket.UpdatedAt, before, DateTime.UtcNow);
        Assert.Equal(1, host.UnitOfWork.SaveCalls);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[] { VendorTicketStorageBoundary.Signed("receipts/first.png"), VendorTicketStorageBoundary.Signed(key) },
            json.RootElement.GetProperty("proof_urls").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task ReceiptPost_InitializesNullProofListAndAcceptsTheFiveMegabyteBoundary()
    {
        await using var host = await VendorTicketHost.CreateAsync();
        host.Tickets[0].ProofUrls = null;
        using var form = Proof("proof.png", "image/png", 5 * 1024 * 1024);
        using var response = await host.SendAsync(HttpMethod.Post, Receipt, form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(host.Tickets[0].ProofUrls!);
        Assert.Equal(1, host.UnitOfWork.SaveCalls);
    }

    [Theory]
    [InlineData("proof.gif", "image/gif", 16)]
    [InlineData("proof.png", "image/jpeg", 16)]
    [InlineData("proof.jpg", "image/png", 16)]
    [InlineData("proof.png", "image/png", 0)]
    [InlineData("proof.png", "image/png", 5242881)]
    public async Task ReceiptPost_RejectsInvalidFilesBeforeUploadingOrSaving(string filename, string mime, int length)
    {
        await using var host = await VendorTicketHost.CreateAsync();
        using var form = Proof(filename, mime, length);
        using var response = await host.SendAsync(HttpMethod.Post, Receipt, form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(host.Storage.UploadedKeys);
        Assert.Equal(0, host.UnitOfWork.SaveCalls);
        Assert.Null(host.Tickets[0].ProofUrls);
    }

    [Fact]
    public async Task ReceiptPost_RequiresProofFileAndMultipartContentType()
    {
        await using var host = await VendorTicketHost.CreateAsync();
        using var form = new MultipartFormDataContent();
        using var missing = await host.SendAsync(HttpMethod.Post, Receipt, form);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var json = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var wrongFormat = await host.SendAsync(HttpMethod.Post, Receipt, json);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrongFormat.StatusCode);
        Assert.Empty(host.Storage.UploadedKeys);
        Assert.Equal(0, host.UnitOfWork.SaveCalls);
    }

    [Fact]
    public async Task CommunityGet_ReturnsSortedSignedLogsAndTheSameProgressAsEnforcer()
    {
        await using var host = await VendorTicketHost.CreateAsync(configureTickets: tickets =>
        {
            tickets[0].PenaltyType = PenaltyType.CommunityService;
            tickets[0].CommunityServiceHours = 8;
            tickets[0].CommunityServiceLogs =
            [
                new() { ServiceDate = new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc), HoursWorked = 3m, ProofUrl = "service/second.png" },
                new() { ServiceDate = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc), HoursWorked = 2.5m, ProofUrl = "service/first.png" }
            ];
        });
        using var response = await host.GetAsync(Community);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(8, json.RootElement.GetProperty("hours_required").GetInt32());
        Assert.Equal(5.5m, json.RootElement.GetProperty("hours_completed").GetDecimal());
        Assert.Equal(2.5m, json.RootElement.GetProperty("hours_remaining").GetDecimal());
        Assert.Equal(68.8m, json.RootElement.GetProperty("completion_percentage").GetDecimal());
        Assert.Equal("2026-10-13T00:00:00Z", json.RootElement.GetProperty("entries")[0].GetProperty("service_date").GetString());
        Assert.Equal(VendorTicketStorageBoundary.Signed("service/first.png"), json.RootElement.GetProperty("entries")[0].GetProperty("proof_url").GetString());
        using var enforcer = await host.GetAsync("/api/enforcer/tickets/101/settlement/community-service", role: "MarketEnforcer", userId: "12");
        Assert.Equal(HttpStatusCode.OK, enforcer.StatusCode);
        Assert.Equal(body, await enforcer.Content.ReadAsStringAsync());
        Assert.Equal(0, host.UnitOfWork.SaveCalls);
        Assert.Empty(host.Storage.UploadedKeys);
    }

    [Fact]
    public async Task CommunityPost_IsNotAllowed()
    {
        await using var host = await VendorTicketHost.CreateAsync();
        using var response = await host.SendAsync(HttpMethod.Post, Community);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Empty(host.Storage.UploadedKeys);
        Assert.Equal(0, host.UnitOfWork.SaveCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(null)]
    public async Task CommunityGet_RejectsInvalidRequiredHours(int? hours)
    {
        await using var host = await VendorTicketHost.CreateAsync(configureTickets: tickets =>
        {
            tickets[0].PenaltyType = PenaltyType.CommunityService;
            tickets[0].CommunityServiceHours = hours;
        });
        using var response = await host.GetAsync(Community);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, host.Storage.Calls);
    }

    [Theory]
    [InlineData("receipt", false, "901", 404)]
    [InlineData("receipt", true, "901", 404)]
    [InlineData("community-service", false, "901", 404)]
    [InlineData("receipt", false, "99999", 404)]
    [InlineData("receipt", true, "99999", 404)]
    [InlineData("community-service", false, "99999", 404)]
    [InlineData("receipt", false, "0", 400)]
    [InlineData("receipt", true, "0", 400)]
    [InlineData("community-service", false, "0", 400)]
    public async Task OwnershipAndIds_AreValidatedBeforeStorageAccess(string action, bool post, string id, int expected)
    {
        await using var host = await VendorTicketHost.CreateAsync();
        using var form = Proof("proof.png", "image/png");
        using var response = await host.SendAsync(post ? HttpMethod.Post : HttpMethod.Get,
            $"/api/vendor/tickets/{id}/settlement/{action}?user_id=88&vendor_id=7", post ? form : null);
        Assert.Equal(expected, (int)response.StatusCode);
        if (expected == 404)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Ticket not found.", json.RootElement.GetProperty("message").GetString());
        }
        Assert.Equal(0, host.Storage.Calls);
        Assert.Empty(host.Storage.UploadedKeys);
        Assert.Equal(0, host.UnitOfWork.SaveCalls);
    }

    [Theory]
    [InlineData("receipt", false)]
    [InlineData("receipt", true)]
    [InlineData("community-service", false)]
    public async Task AllRoutes_RejectInvalidClaimsAndWrongRoles(string action, bool post)
    {
        await using var host = await VendorTicketHost.CreateAsync();
        foreach (var (role, userId, expected) in new (string?, string?, int)[]
            { (null, "7", 401), ("MarketEnforcer", "7", 403), ("AdminOfficer", "7", 403), ("MarketVendor", null, 401), ("MarketVendor", "invalid", 401) })
        {
            using var form = Proof("proof.png", "image/png");
            using var response = await host.SendAsync(post ? HttpMethod.Post : HttpMethod.Get,
                $"/api/vendor/tickets/101/settlement/{action}", post ? form : null, role, userId);
            Assert.Equal(expected, (int)response.StatusCode);
        }
        Assert.Equal(0, host.Storage.Calls);
        Assert.Empty(host.Storage.UploadedKeys);
        Assert.Equal(0, host.UnitOfWork.SaveCalls);
    }

    [Theory]
    [InlineData("receipt", false)]
    [InlineData("receipt", true)]
    [InlineData("community-service", false)]
    public async Task Settlement_RejectsWarningsMissingPenaltyAndWrongPenaltyType(string action, bool post)
    {
        foreach (var scenario in new[] { "warning", "missing", "wrong" })
        {
            await using var host = await VendorTicketHost.CreateAsync(configureTickets: tickets =>
            {
                var ticket = tickets[0];
                if (scenario == "warning") ticket.Type = ViolationType.Warning;
                if (scenario == "missing") ticket.PenaltyType = null;
                if (scenario == "wrong") ticket.PenaltyType = action == "receipt" ? PenaltyType.CommunityService : PenaltyType.CashFine;
            });
            using var form = Proof("proof.png", "image/png");
            using var response = await host.SendAsync(post ? HttpMethod.Post : HttpMethod.Get,
                $"/api/vendor/tickets/101/settlement/{action}", post ? form : null);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Empty(host.Storage.UploadedKeys);
            Assert.Equal(0, host.Storage.Calls);
            Assert.Equal(0, host.UnitOfWork.SaveCalls);
        }
    }

    [Fact]
    public async Task TrackedOwnedSettlementQuery_TranslatesToPostgres()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        var fixture = new VendorTicketDbSet<Ticket>(VendorTicketHost.Records());
        context.Tickets = fixture;
        var repository = new TicketRepository(context);
        var method = typeof(TicketRepository).GetMethod("GetVendorTicketByIdAsync");
        Assert.NotNull(method);
        await (Task)method.Invoke(repository, [101, 7])!;
        using var postgres = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").UseSnakeCaseNamingConvention().Options);
        var expression = new ReplaceReportQueryRoot(fixture.RootExpression,
            ((IQueryable<Ticket>)postgres.Tickets).Expression).Visit(fixture.State.LastExpression!);
        var sql = ((IQueryable<Ticket>)postgres.Tickets).Provider.CreateQuery(expression!).ToQueryString();
        Assert.Contains("user_id", sql);
        Assert.Contains("WHERE", sql);
    }

    private static MultipartFormDataContent Proof(string filename, string mime, int length = 16)
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(new byte[length]);
        content.Headers.ContentType = new MediaTypeHeaderValue(mime);
        form.Add(content, "proof_file", filename);
        return form;
    }
}
