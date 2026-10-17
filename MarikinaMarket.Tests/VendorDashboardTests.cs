using MarikinaMarket.API.Presentation.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;
using System.Security.Claims;
using MarikinaMarket.API.Application;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using MarikinaMarket.API.Infrastructure.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarikinaMarket.Tests;

public class VendorDashboardTests
{
    [Fact]
    public async Task Dashboard_counts_all_history_but_tasks_only_include_owned_active_tickets()
    {
        var vendor = new VendorProfile { Id = 70, UserId = 7, BusinessId = "MM-70", BusinessName = "Fresh Fish", QrCodeValue = "synthetic" };
        var other = new VendorProfile { Id = 7, UserId = 88, BusinessId = "MM-7", BusinessName = "Other", QrCodeValue = "other" };
        var records = Enum.GetValues<TicketStatus>().Select((status, index) => new Ticket
        {
            Id = index + 1, VendorId = vendor.Id, Vendor = vendor, Type = ViolationType.Ticket,
            Status = status, Description = "Synthetic inspection", ControlNumber = $"TKT-{index + 1:D4}",
            PenaltyType = status == TicketStatus.InProgress ? PenaltyType.CommunityService :
                status == TicketStatus.Contested ? PenaltyType.BloodDonation : PenaltyType.CashFine,
            CommunityServiceHours = status == TicketStatus.InProgress ? 8 : null,
            IssuedAt = DateTime.UtcNow.AddDays(-500),
            CommunityServiceLogs = status == TicketStatus.InProgress ?
                [new CommunityServiceLog { HoursWorked = 2.5m, ProofUrl = "synthetic" }] : []
        }).ToList();
        records.Add(new Ticket { Id = 20, VendorId = 70, Vendor = vendor, Type = ViolationType.Warning, Description = "Warning" });
        records.Add(new Ticket { Id = 30, VendorId = 7, Vendor = other, Type = ViolationType.Ticket,
            Status = TicketStatus.Pending, PenaltyType = PenaltyType.CashFine, Description = "Other account" });
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options)
        {
            Tickets = new VendorTicketDbSet<Ticket>(records),
            VendorProfiles = new VendorSelfProfileDatabaseBoundary([vendor, other])
        };
        var ticketRepository = new TicketRepository(context);
        var vendorRepository = new VendorRepository(context);
        var unit = ReviewBoundaryProxy.Create<IUnitOfWork>((method, _) => method.Name == "SaveChangesAsync"
            ? Task.FromResult(0) : Task.CompletedTask);
        var compliance = new VendorComplianceScoreService(vendorRepository, ticketRepository, unit);
        var service = new VendorService(null!, vendorRepository, unit, null!, compliance, null!, null!, null!,
            NullLogger<VendorService>.Instance, ticketRepository: ticketRepository);

        var result = await service.GetOwnDashboardAsync(7);

        Assert.Equal(1, result.TotalWarnings);
        Assert.Equal(7, result.TotalTickets);
        Assert.Equal(new[] { 7, 5, 4, 1 }, result.Tasks.Select(task => task.TicketId));
        Assert.Contains(result.Tasks, task => task.PenaltyType == PenaltyType.BloodDonation);
        var progress = Assert.Single(result.Tasks, task => task.PenaltyType == PenaltyType.CommunityService);
        Assert.Equal(8, progress.HoursRequired);
        Assert.Equal(2.5m, progress.HoursCompleted);
        // All owned tickets are outside the admin's 365-day score window.
        Assert.Equal(100, result.Compliance.ComplianceScore);
        Assert.Equal("Good Standing", result.Compliance.Standing);
        Assert.Null(result.Compliance.DaysSinceLastTicket);
        Assert.Equal(100, vendor.ComplianceScore);
        records[0].IssuedAt = DateTime.UtcNow;
        var recent = await service.GetOwnDashboardAsync(7);
        // One low-severity ticket today: .30*80 + .20*0 + .30*100 + .20*100 = 74.
        Assert.Equal(74, recent.Compliance.ComplianceScore);
        Assert.Equal("Fair Standing", recent.Compliance.Standing);
        Assert.Equal(74, vendor.ComplianceScore);
        records.Clear();
        var empty = await service.GetOwnDashboardAsync(7);
        Assert.Equal(0, empty.TotalWarnings);
        Assert.Equal(0, empty.TotalTickets);
        Assert.Empty(empty.Tasks);
        Assert.Equal(100, empty.Compliance.ComplianceScore);
        await Assert.ThrowsAsync<RecordNotFoundException>(() => service.GetOwnDashboardAsync(123));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("0")]
    public async Task Dashboard_rejects_missing_or_invalid_identity(string? id)
    {
        var controller = new VendorController(null!)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        if (id is not null)
            controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)]));
        Assert.IsType<UnauthorizedObjectResult>((await controller.GetOwnDashboard()).Result);
    }

    [Fact]
    public void Dashboard_is_a_vendor_only_get_endpoint()
    {
        var action = typeof(VendorController).GetMethods().SingleOrDefault(method =>
            method.GetCustomAttributes(typeof(HttpGetAttribute), false)
                .Cast<HttpGetAttribute>().Any(attribute => attribute.Template == "dashboard"));
        Assert.NotNull(action);
        Assert.Equal("MarketVendor", action.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().Single().Roles);
    }
}
