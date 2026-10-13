using System.Net;
using System.Text.Json;
using MarikinaMarket.API.Infrastructure.Persistence;
using MarikinaMarket.API.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MarikinaMarket.Tests;

public class VendorTicketDetailTests
{
    [Fact]
    public async Task OwnedTicket_ReturnsTheSameJsonAsEnforcerDetailAndSignsEvidence()
    {
        await using var host = await VendorTicketHost.CreateAsync();
        using var vendor = await host.GetAsync("/api/vendor/tickets/101?user_id=88&vendor_id=7");
        Assert.Equal(HttpStatusCode.OK, vendor.StatusCode);
        var vendorJson = await vendor.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(vendorJson);
        var detail = json.RootElement;
        Assert.Equal(101, detail.GetProperty("ticket_id").GetInt32());
        Assert.Equal(70, detail.GetProperty("vendor_id").GetInt32());
        Assert.Equal("Reyes Fresh Seafood", detail.GetProperty("business_name").GetString());
        Assert.Equal("Juan", detail.GetProperty("first_name").GetString());
        Assert.Equal("Reyes", detail.GetProperty("last_name").GetString());
        Assert.Equal("Fish and Seafood Section", detail.GetProperty("market_section_name").GetString());
        Assert.Equal("Public", detail.GetProperty("vendor_type").GetString());
        Assert.Equal("F-014", detail.GetProperty("stall_number").GetString());
        Assert.Equal("Pending", detail.GetProperty("status").GetString());
        Assert.Equal("2026-10-18T00:01:00Z", detail.GetProperty("due_date").GetString());
        Assert.Equal("CashFine", detail.GetProperty("penalty_type").GetString());
        Assert.Equal("Minor", detail.GetProperty("severity").GetString());
        Assert.Equal(1000m, detail.GetProperty("total_fine_amount").GetDecimal());
        Assert.Equal(new[] { "Licensing" }, detail.GetProperty("categories").EnumerateArray().Select(value => value.GetString()));
        var violation = detail.GetProperty("violations")[0];
        Assert.Equal(2, violation.GetProperty("ordinance_id").GetInt32());
        Assert.Equal("104", violation.GetProperty("ordinance_no").GetString());
        Assert.Equal(1, violation.GetProperty("offense_count").GetInt32());
        Assert.Equal(500m, violation.GetProperty("penalty_amount").GetDecimal());
        Assert.Equal(new[] { VendorTicketStorageBoundary.Signed("tickets/2026/10/101-a.jpg"), VendorTicketStorageBoundary.Signed("tickets/2026/10/101-b.jpg") },
            detail.GetProperty("ticket_evidences").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(1, host.Storage.Calls);
        Assert.Equal(new[] { "tickets/2026/10/101-a.jpg", "tickets/2026/10/101-b.jpg" }, host.Storage.SignedKeys);
        Assert.False(detail.TryGetProperty("overdue_date", out _));
        Assert.False(detail.TryGetProperty("ticket_status", out _));
        using var enforcer = await host.GetAsync("/api/enforcer/tickets/101", role: "MarketEnforcer");
        Assert.Equal(HttpStatusCode.OK, enforcer.StatusCode);
        Assert.Equal(await enforcer.Content.ReadAsStringAsync(), vendorJson);
    }

    [Fact]
    public async Task Warning_ReturnsNullPenaltyFieldsAndNoEvidenceLookup()
    {
        await using var host = await VendorTicketHost.CreateAsync();
        using var response = await host.GetAsync("/api/vendor/tickets/200");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Warning", json.RootElement.GetProperty("type").GetString());
        foreach (var field in new[] { "control_number", "status", "due_date", "severity", "penalty_type", "total_fine_amount" })
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty(field).ValueKind);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("violations")[0].GetProperty("penalty_amount").ValueKind);
        Assert.Equal(0, json.RootElement.GetProperty("ticket_evidences").GetArrayLength());
        Assert.Equal(0, host.Storage.Calls);
    }

    [Theory]
    [InlineData(901, "7")]
    [InlineData(101, "88")]
    [InlineData(99999, "7")]
    [InlineData(101, "999")]
    public async Task MissingOrOtherVendorsTicket_Returns404BeforeSigningEvidence(int ticketId, string userId)
    {
        await using var host = await VendorTicketHost.CreateAsync();
        using var response = await host.GetAsync($"/api/vendor/tickets/{ticketId}?user_id=88&vendor_id=7", userId: userId);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Ticket not found.", json.RootElement.GetProperty("message").GetString());
        Assert.Equal(0, host.Storage.Calls);
        Assert.Empty(host.Storage.SignedKeys);
    }

    [Fact]
    public async Task DifferentVendor_CanReadItsOwnPrivateVendorTicket()
    {
        await using var host = await VendorTicketHost.CreateAsync();
        using var response = await host.GetAsync("/api/vendor/tickets/901", userId: "88");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(7, json.RootElement.GetProperty("vendor_id").GetInt32());
        Assert.Equal("Private", json.RootElement.GetProperty("vendor_type").GetString());
        Assert.Equal("Santos Meat Shop", json.RootElement.GetProperty("business_name").GetString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("invalid")]
    [InlineData("2147483648")]
    public async Task InvalidTicketId_Returns400(string ticketId)
    {
        await using var host = await VendorTicketHost.CreateAsync();
        using var response = await host.GetAsync($"/api/vendor/tickets/{ticketId}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, host.Storage.Calls);
    }

    [Theory]
    [InlineData(null, "7", 401)]
    [InlineData("MarketEnforcer", "7", 403)]
    [InlineData("AdminOfficer", "7", 403)]
    [InlineData("HeadAdmin", "7", 403)]
    [InlineData("MarketVendor", null, 401)]
    [InlineData("MarketVendor", "invalid", 401)]
    [InlineData("MarketVendor", "0", 401)]
    [InlineData("MarketVendor", "-1", 401)]
    public async Task InvalidRoleOrClaims_ReturnExpectedStatus(string? role, string? userId, int status)
    {
        await using var host = await VendorTicketHost.CreateAsync();
        using var response = await host.GetAsync("/api/vendor/tickets/101", role, userId);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(0, host.Storage.Calls);
    }

    [Fact]
    public async Task ExpiredJwt_Returns401()
    {
        await using var host = await VendorTicketHost.CreateAsync();
        using var response = await host.GetAsync("/api/vendor/tickets/101", expired: true);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, host.Storage.Calls);
    }

    [Fact]
    public async Task OwnedDetailProjection_TranslatesToPostgres()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        var fixture = new VendorTicketDbSet<MarikinaMarket.API.Domain.Entities.Ticket>(VendorTicketHost.Records());
        context.Tickets = fixture;
        var repository = new TicketRepository(context);
        var method = typeof(TicketRepository).GetMethod("GetVendorTicketDetailAsync");
        Assert.NotNull(method);
        await (Task)method.Invoke(repository, [101, 7])!;
        using var postgres = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").UseSnakeCaseNamingConvention().Options);
        var expression = new ReplaceReportQueryRoot(fixture.RootExpression,
            ((IQueryable<MarikinaMarket.API.Domain.Entities.Ticket>)postgres.Tickets).Expression).Visit(fixture.State.LastExpression!);
        var sql = ((IQueryable<MarikinaMarket.API.Domain.Entities.Ticket>)postgres.Tickets).Provider.CreateQuery(expression!).ToQueryString();
        Assert.Contains("user_id", sql);
        Assert.Contains("penalty_amount", sql);
        Assert.Contains("file_key", sql);
        Assert.Contains("WHERE", sql);
    }
}
