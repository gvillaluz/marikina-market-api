using System.Reflection;
using System.Net;
using System.Text.Json;
using MarikinaMarket.API.Application.DTOs.Analytics.Internal;
using MarikinaMarket.API.Application.DTOs.Analytics.Response;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Presentation.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Module = MarikinaMarket.API.Domain.Enums.Module;

namespace MarikinaMarket.Tests;

public class DashboardSummaryTests
{
    [Fact]
    public async Task GetSummaryAsync_CombinesOwnerRepositoryDataAndMapsRecentActivity()
    {
        var tickets = DashboardTicketRepositoryStub.Create();
        ((DashboardTicketRepositoryStub)(object)tickets).Counts = new DashboardTicketCounts
        {
            InspectionsToday = 12,
            OpenTickets = 8
        };
        var vendors = DashboardVendorRepositoryStub.Create();
        ((DashboardVendorRepositoryStub)(object)vendors).Counts = new DashboardVendorCounts
        {
            ActiveVendors = 247,
            PendingRegistrations = 4
        };
        var auditLogs = DashboardAuditRepositoryStub.Create();
        ((DashboardAuditRepositoryStub)(object)auditLogs).Activities =
        [
            new AuditLog
            {
                Id = 1101,
                Timestamp = DateTime.UtcNow,
                UserId = 8,
                Action = "SaveNewInspection",
                Module = Module.Tickets,
                Result = LogResult.Success,
                Details = "Inspection recorded for Market Section A."
            }
        ];
        var users = AuditUserRepositoryStub.Create();
        var service = new DashboardService(tickets, vendors, auditLogs, users);

        var result = await service.GetSummaryAsync();

        Assert.Equal(12, result.Metrics.InspectionsToday);
        Assert.Equal(8, result.Metrics.OpenTickets);
        Assert.Equal(247, result.Metrics.ActiveVendors);
        Assert.Equal(4, result.Metrics.PendingRegistrations);
        Assert.Equal(DateTimeKind.Utc, result.AsOf.Kind);
        Assert.Equal(2, result.AttentionItems.Count);
        Assert.Equal("open-tickets", result.AttentionItems[0].Id);
        Assert.Equal("pending-registrations", result.AttentionItems[1].Id);
        var activity = Assert.Single(result.RecentActivity);
        Assert.Equal("Inspection", activity.Category);
        Assert.Equal("Inspection completed", activity.Title);
        Assert.Equal("Maria Santos", activity.ActorName);
        Assert.Equal(5, ((DashboardAuditRepositoryStub)(object)auditLogs).Limit);

        var ticketStub = (DashboardTicketRepositoryStub)(object)tickets;
        Assert.Equal(TimeSpan.FromDays(1), ticketStub.EndUtc - ticketStub.StartUtc);
        Assert.Equal(DateTimeKind.Utc, ticketStub.StartUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, ticketStub.EndUtc.Kind);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsEmptyAttentionAndActivityForZeroCounts()
    {
        var tickets = DashboardTicketRepositoryStub.Create();
        var vendors = DashboardVendorRepositoryStub.Create();
        var auditLogs = DashboardAuditRepositoryStub.Create();
        var users = AuditUserRepositoryStub.Create();
        var service = new DashboardService(tickets, vendors, auditLogs, users);

        var result = await service.GetSummaryAsync();

        Assert.Empty(result.AttentionItems);
        Assert.Empty(result.RecentActivity);
        Assert.Equal(0, result.Metrics.InspectionsToday);
        Assert.Equal(0, result.Metrics.OpenTickets);
        Assert.Equal(0, result.Metrics.ActiveVendors);
        Assert.Equal(0, result.Metrics.PendingRegistrations);
        Assert.Empty(((AuditUserRepositoryStub)(object)users).NameRequests);
    }

    [Fact]
    public void DashboardSummaryEndpoint_RestrictsRouteToAdministratorRoles()
    {
        var controller = typeof(DashboardController);
        var authorize = controller.GetCustomAttribute<AuthorizeAttribute>();
        var route = controller.GetCustomAttribute<RouteAttribute>();
        var get = controller.GetMethod(nameof(DashboardController.GetSummary))!
            .GetCustomAttribute<HttpGetAttribute>();

        Assert.Equal("HeadAdmin,AdminOfficer", authorize?.Roles);
        Assert.Equal("api/dashboard", route?.Template);
        Assert.Equal("summary", get?.Template);
    }

    [Theory]
    [InlineData("HeadAdmin", HttpStatusCode.OK)]
    [InlineData("AdminOfficer", HttpStatusCode.OK)]
    [InlineData("MarketEnforcer", HttpStatusCode.Forbidden)]
    [InlineData("MarketVendor", HttpStatusCode.Forbidden)]
    public async Task DashboardSummaryEndpoint_EnforcesAdministratorRoles(string role, HttpStatusCode expected)
    {
        await using var host = await AuditHost.CreateAsync(services =>
            services.AddSingleton<IDashboardService, DashboardServiceStub>());

        var response = await host.SendAsync(HttpMethod.Get, "/api/dashboard/summary", role);

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.TryGetProperty("metrics", out _));
            Assert.True(json.RootElement.TryGetProperty("recent_activity", out _));
            Assert.True(json.RootElement.TryGetProperty("attention_items", out _));
        }
    }

    [Fact]
    public async Task DashboardSummaryEndpoint_RequiresAuthentication()
    {
        await using var host = await AuditHost.CreateAsync(services =>
            services.AddSingleton<IDashboardService, DashboardServiceStub>());

        var response = await host.SendAsync(HttpMethod.Get, "/api/dashboard/summary", role: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public sealed class DashboardServiceStub : IDashboardService
{
    public Task<DashboardSummaryResponse> GetSummaryAsync()
        => Task.FromResult(new DashboardSummaryResponse
        {
            AsOf = DateTime.UtcNow,
            Metrics = new DashboardMetricsResponse()
        });
}

public class DashboardTicketRepositoryStub : DispatchProxy
{
    public DashboardTicketCounts Counts { get; set; } = new();
    public DateTime StartUtc { get; private set; }
    public DateTime EndUtc { get; private set; }

    public static ITicketRepository Create()
        => DispatchProxy.Create<ITicketRepository, DashboardTicketRepositoryStub>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == nameof(ITicketRepository.GetDashboardTicketCountsAsync))
        {
            StartUtc = (DateTime)args![0]!;
            EndUtc = (DateTime)args[1]!;
            return Task.FromResult(Counts);
        }
        throw new InvalidOperationException($"Unexpected ticket repository call: {targetMethod?.Name}");
    }
}

public class DashboardVendorRepositoryStub : DispatchProxy
{
    public DashboardVendorCounts Counts { get; set; } = new();

    public static IVendorRepository Create()
        => DispatchProxy.Create<IVendorRepository, DashboardVendorRepositoryStub>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == nameof(IVendorRepository.GetDashboardVendorCountsAsync))
            return Task.FromResult(Counts);
        throw new InvalidOperationException($"Unexpected vendor repository call: {targetMethod?.Name}");
    }
}

public class DashboardAuditRepositoryStub : DispatchProxy
{
    public List<AuditLog> Activities { get; set; } = [];
    public int Limit { get; private set; }

    public static IAuditLogRepository Create()
        => DispatchProxy.Create<IAuditLogRepository, DashboardAuditRepositoryStub>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == nameof(IAuditLogRepository.GetRecentDashboardActivitiesAsync))
        {
            Limit = (int)args![0]!;
            return Task.FromResult(Activities);
        }
        throw new InvalidOperationException($"Unexpected audit repository call: {targetMethod?.Name}");
    }
}
