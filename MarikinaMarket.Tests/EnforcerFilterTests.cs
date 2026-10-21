using System.Net;
using System.Text.Json;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using MarikinaMarket.API.Infrastructure.Repositories;
using MarikinaMarket.API.Presentation.Controllers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MarikinaMarket.Tests;

public class EnforcerFilterTests
{
    [Theory]
    [InlineData("/api/enforcer/tickets/inspections")]
    [InlineData("/api/enforcer/tickets")]
    public async Task Http_all_filter_binds_and_remains_enforcer_owned(string route)
    {
        await using var host = await VendorTicketHost.CreateAsync(configureTickets: records =>
        {
            records[0].EnforcerId = 99;
            records.Single(t => t.Id == 901).Status = TicketStatus.Paid;
        });
        using var response = await host.GetAsync(route, role: "MarketEnforcer", userId: "12");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = json.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(10, items.Length);
        Assert.True(json.RootElement.GetProperty("has_more").GetBoolean());
        Assert.All(items, item => Assert.Equal(12, item.GetProperty("enforcer_id").GetInt32()));
        Assert.Contains(items, item => item.GetProperty("status").GetString() == "Paid");
        if (route.EndsWith("inspections"))
            Assert.Contains(items, item => item.GetProperty("type").GetString() == "Warning");
        using var otherOwner = await host.GetAsync(route + "?offset=10", role: "MarketEnforcer", userId: "12");
        Assert.Equal(HttpStatusCode.OK, otherOwner.StatusCode);
        using var isolated = JsonDocument.Parse(await otherOwner.Content.ReadAsStringAsync());
        // The oldest record belongs to another enforcer and must not appear on the final page.
        Assert.DoesNotContain(isolated.RootElement.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("control_number").GetString() == "101");
    }

    [Theory]
    [InlineData("/api/enforcer/tickets/inspections?type=invalid", "MarketEnforcer", 400)]
    [InlineData("/api/enforcer/tickets?status=invalid", "MarketEnforcer", 400)]
    [InlineData("/api/enforcer/tickets/inspections", "MarketVendor", 403)]
    [InlineData("/api/enforcer/tickets", "MarketVendor", 403)]
    [InlineData("/api/enforcer/tickets/inspections", null, 401)]
    [InlineData("/api/enforcer/tickets", null, 401)]
    public async Task Http_filter_errors_and_authorization_are_preserved(string route, string? role, int expected)
    {
        await using var host = await VendorTicketHost.CreateAsync();
        using var response = await host.GetAsync(route, role: role, userId: "12");
        Assert.Equal((HttpStatusCode)expected, response.StatusCode);
    }

    [Fact]
    public async Task Every_existing_ticket_status_is_supported()
    {
        foreach (var status in Enum.GetValues<TicketStatus>())
        {
            using var context = Context();
            foreach (var ticket in context.Tickets.Where(t => t.Type == ViolationType.Ticket)) ticket.Status = status;
            var page = await Tickets(Service(context), status, "Market");
            Assert.Equal(6, page.Items.Count);
            Assert.All(page.Items, item => Assert.Equal(status, item.Status));
        }
    }

    [Theory]
    [InlineData(nameof(EnforcerTicketController.GetAllInspectionsByEnforcerId), "type")]
    [InlineData(nameof(EnforcerTicketController.GetAllTicketsByEnforcerId), "status")]
    public void Omitted_filter_means_all(string method, string parameter)
    {
        var filter = typeof(EnforcerTicketController).GetMethod(method)!.GetParameters().Single(p => p.Name == parameter);
        Assert.Null(filter.DefaultValue);
        Assert.NotNull(Nullable.GetUnderlyingType(filter.ParameterType));
    }

    [Fact]
    public async Task All_inspections_include_both_types_and_page_only_owned_records()
    {
        using var context = Context();
        var service = Service(context);
        var page = await Inspections(service, null, 0, "Market");
        Assert.Equal(Enumerable.Range(1, 10), page.Items.Select(i => i.Id));
        Assert.True(page.HasMore);
        Assert.Contains(page.Items, i => i.Type == ViolationType.Warning);
        Assert.Contains(page.Items, i => i.Type == ViolationType.Ticket);
        var next = await Inspections(service, null, 10, "Market");
        Assert.Equal(new[] { 11, 12 }, next.Items.Select(i => i.Id));
        Assert.False(next.HasMore);
    }

    [Fact]
    public async Task All_ticket_statuses_exclude_warnings_and_other_enforcers()
    {
        using var context = Context();
        var page = await Tickets(Service(context), null, "Market");
        Assert.Equal(new[] { 2, 4, 6, 8, 10, 12 }, page.Items.Select(i => i.Id));
        Assert.Contains(page.Items, i => i.Status == TicketStatus.Paid);
        Assert.Contains(page.Items, i => i.Status == TicketStatus.Pending);
    }

    [Theory]
    [InlineData(ViolationType.Warning, 1)]
    [InlineData(ViolationType.Ticket, 2)]
    public async Task Explicit_type_combines_with_search(ViolationType type, int id)
    {
        using var context = Context();
        var page = await Inspections(Service(context), type, 0, $"CONTROL-{id:D2}");
        Assert.Equal(id, Assert.Single(page.Items).Id);
        Assert.False(page.HasMore);
        Assert.Empty((await Inspections(Service(context), type, 0, "no matching vendor")).Items);
    }

    [Theory]
    [InlineData(TicketStatus.Pending, 2)]
    [InlineData(TicketStatus.Paid, 4)]
    public async Task Explicit_status_combines_with_search(TicketStatus status, int id)
    {
        using var context = Context();
        var page = await Tickets(Service(context), status, $"CONTROL-{id:D2}");
        Assert.Equal(id, Assert.Single(page.Items).Id);
        Assert.False(page.HasMore);
        Assert.Empty((await Tickets(Service(context), status, "no matching vendor")).Items);
    }

    private static TicketService Service(AppDbContext context) => new(new TicketRepository(context), null!, null!, null!, null!, null!, null!);
    private static Task<PageResponse<InspectionSummaryResponse>> Inspections(TicketService service, ViolationType? type, int offset, string search) =>
        service.GetInspectionsByEnforcerIdAsync(8, offset, type, search);
    private static Task<PageResponse<TicketSummaryResponse>> Tickets(TicketService service, TicketStatus? status, string search) =>
        service.GetTicketsByEnforcerIdAsync(8, 0, status, search);

    private static AppDbContext Context()
    {
        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        context.Tickets = new AccountDatabaseSet<Ticket>(Enumerable.Range(1, 14).Select(id => new Ticket
        {
            Id = id, EnforcerId = id <= 12 ? 8 : 9, VendorId = 17, ControlNumber = $"CONTROL-{id:D2}",
            Type = id % 2 == 0 ? ViolationType.Ticket : ViolationType.Warning,
            Status = id % 2 == 0 ? (id % 4 == 0 ? TicketStatus.Paid : TicketStatus.Pending) : null,
            IssuedAt = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc).AddMinutes(-id),
            Description = "Synthetic market inspection",
            Vendor = new VendorProfile { BusinessName = "Market Goods", BusinessId = "BUS-17", QrCodeValue = "synthetic-17",
                User = new User { FirstName = "Juan", LastName = "Santos", DateOfBirth = new DateOnly(1990, 1, 1),
                    HouseNumber = "1", Street = "Test Street", Barangay = "Test Barangay", City = "Marikina" } },
            MarketSection = new MarketSection { Name = "Dry Goods", Description = "Synthetic section" },
            TicketViolations = []
        }).ToList());
        return context;
    }
}
