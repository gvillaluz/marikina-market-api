using System.Net;
using System.Reflection;
using System.Text.Json;
using MarikinaMarket.API.Application;
using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MarikinaMarket.Tests;

public class TicketStatusSettlementTests
{
    [Theory]
    [InlineData(PenaltyType.CashFine, TicketStatus.Paid)]
    [InlineData(PenaltyType.CashFine, TicketStatus.Cleared)]
    [InlineData(PenaltyType.BloodDonation, TicketStatus.Paid)]
    [InlineData(PenaltyType.BloodDonation, TicketStatus.Cleared)]
    public async Task Receipt_settlement_requires_a_nonblank_proof(PenaltyType penalty, TicketStatus status)
    {
        foreach (var proofs in new List<string>?[] { null, [], ["", " "] })
        {
            var fixture = new Fixture(penalty);
            fixture.Ticket.ProofUrls = proofs;
            var error = await Assert.ThrowsAsync<InvalidRequestException>(() => fixture.UpdateAsync(status));
            Assert.Equal("Settlement proof is required before marking this ticket Paid or Cleared.", error.Message);
            fixture.AssertRejectedWithoutMutation();
        }

        var valid = new Fixture(penalty);
        valid.Ticket.ProofUrls = ["", "receipt/proof.png"];
        var response = await valid.UpdateAsync(status);
        Assert.Equal(status, response.Status);
        Assert.NotNull(valid.Ticket.ResolvedAt);
        Assert.Contains("CommitAsync", valid.Calls);
        Assert.Contains("SaveNotificationAsync", valid.Calls);
    }

    [Theory]
    [InlineData(TicketStatus.Paid, 0, false)]
    [InlineData(TicketStatus.Paid, 7.5, false)]
    [InlineData(TicketStatus.Paid, 8, true)]
    [InlineData(TicketStatus.Paid, 8.5, true)]
    [InlineData(TicketStatus.Cleared, 0, false)]
    [InlineData(TicketStatus.Cleared, 7.5, false)]
    [InlineData(TicketStatus.Cleared, 8, true)]
    [InlineData(TicketStatus.Cleared, 8.5, true)]
    public async Task Community_service_requires_completed_hours(TicketStatus status, double hours, bool allowed)
    {
        var fixture = new Fixture(PenaltyType.CommunityService);
        fixture.Ticket.CommunityServiceHours = 8;
        fixture.Ticket.CommunityServiceLogs =
        [
            new CommunityServiceLog { HoursWorked = (decimal)hours / 2, ProofUrl = "first.png" },
            new CommunityServiceLog { HoursWorked = (decimal)hours / 2, ProofUrl = "second.png" }
        ];

        if (allowed)
        {
            var response = await fixture.UpdateAsync(status);
            Assert.Equal(status, response.Status);
            Assert.NotNull(fixture.Ticket.ResolvedAt);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidRequestException>(() => fixture.UpdateAsync(status));
            Assert.Equal("Community service must be completed before marking this ticket Paid or Cleared.", error.Message);
            fixture.AssertRejectedWithoutMutation();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Missing_or_invalid_required_hours_cannot_clear_ticket(int? requiredHours)
    {
        var fixture = new Fixture(PenaltyType.CommunityService);
        fixture.Ticket.CommunityServiceHours = requiredHours;
        await Assert.ThrowsAsync<InvalidRequestException>(() => fixture.UpdateAsync(TicketStatus.Cleared));
        fixture.AssertRejectedWithoutMutation();
    }

    [Theory]
    [InlineData(null)]
    [InlineData((PenaltyType)99)]
    public async Task Unknown_penalty_cannot_resolve_ticket(PenaltyType? penalty)
    {
        var fixture = new Fixture(penalty);
        await Assert.ThrowsAsync<InvalidRequestException>(() => fixture.UpdateAsync(TicketStatus.Paid));
        fixture.AssertRejectedWithoutMutation();
    }

    [Fact]
    public async Task Warning_cannot_be_resolved_as_a_settled_violation()
    {
        var fixture = new Fixture(PenaltyType.CashFine);
        fixture.Ticket.Type = ViolationType.Warning;
        fixture.Ticket.ProofUrls = ["receipt.png"];
        await Assert.ThrowsAsync<InvalidRequestException>(() => fixture.UpdateAsync(TicketStatus.Cleared));
        fixture.AssertRejectedWithoutMutation();
    }

    [Theory]
    [InlineData(TicketStatus.Pending)]
    [InlineData(TicketStatus.Contested)]
    [InlineData(TicketStatus.Overdue)]
    [InlineData(TicketStatus.InProgress)]
    [InlineData(TicketStatus.Waived)]
    public async Task Other_statuses_do_not_require_settlement(TicketStatus status)
    {
        var fixture = new Fixture(PenaltyType.CashFine);
        Assert.Equal(status, (await fixture.UpdateAsync(status)).Status);
    }

    [Fact]
    public async Task Status_endpoint_returns_400_with_settlement_message_and_200_with_snake_case_json()
    {
        var fixture = new Fixture(PenaltyType.CashFine);
        await using var host = await AuditHost.CreateAsync(services => services.AddSingleton<ITicketService>(fixture.Service));
        const string body = "{\"new_status\":\"Paid\",\"version\":7}";
        var rejected = await host.SendAsync(HttpMethod.Patch, "/api/admin/tickets/1/update-status", body: body);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var error = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal("Settlement proof is required before marking this ticket Paid or Cleared.", error.RootElement.GetProperty("message").GetString());
        fixture.AssertRejectedWithoutMutation();

        fixture.Ticket.ProofUrls = ["receipt.png"];
        var accepted = await host.SendAsync(HttpMethod.Patch, "/api/admin/tickets/1/update-status", body: body);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var result = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
        Assert.Equal(1, result.RootElement.GetProperty("ticket_id").GetInt32());
        Assert.Equal("Paid", result.RootElement.GetProperty("status").GetString());
        Assert.Equal("VIO-0001", result.RootElement.GetProperty("control_number").GetString());
        Assert.Equal(8, result.RootElement.GetProperty("version").GetInt32());
    }

    [Theory]
    [InlineData(null, 1, "Paid", HttpStatusCode.Unauthorized)]
    [InlineData("MarketEnforcer", 1, "Paid", HttpStatusCode.Forbidden)]
    [InlineData("HeadAdmin", 2, "Paid", HttpStatusCode.NotFound)]
    [InlineData("HeadAdmin", 1, "UnknownStatus", HttpStatusCode.BadRequest)]
    public async Task Status_endpoint_reports_auth_not_found_and_invalid_status_errors(string? role, int id, string status, HttpStatusCode expected)
    {
        var fixture = new Fixture(PenaltyType.CashFine);
        await using var host = await AuditHost.CreateAsync(services => services.AddSingleton<ITicketService>(fixture.Service));
        var response = await host.SendAsync(HttpMethod.Patch, $"/api/admin/tickets/{id}/update-status", role,
            $"{{\"new_status\":\"{status}\",\"version\":7}}");
        Assert.Equal(expected, response.StatusCode);
        Assert.DoesNotContain("SaveChangesAsync", fixture.Calls);
    }

    [Fact]
    public async Task Status_email_encodes_names_and_control_number()
    {
        var fixture = new Fixture(PenaltyType.CashFine);
        fixture.Ticket.ControlNumber = "<img src=x>";
        fixture.Ticket.Vendor = new VendorProfile
        {
            BusinessId = "business", BusinessName = "Stall", QrCodeValue = "qr",
            User = new User
            {
                FirstName = "<script>alert(1)</script>", LastName = "Smith & Co", Email = "vendor@example.com",
                DateOfBirth = new DateOnly(1990, 1, 1), HouseNumber = "1", Street = "Street",
                Barangay = "Barangay", City = "Marikina"
            }
        };
        await fixture.UpdateAsync(TicketStatus.Contested);
        var html = Assert.Single(fixture.EmailBodies);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("Smith &amp; Co", html);
        Assert.Contains("&lt;img src=x&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img src=x>", html);
    }

    private sealed class Fixture
    {
        public Ticket Ticket { get; }
        public TicketService Service { get; }
        public List<string> Calls { get; } = [];
        public List<string> EmailBodies { get; } = [];

        public Fixture(PenaltyType? penalty)
        {
            Ticket = new Ticket
            {
                Id = 1, ControlNumber = "VIO-0001", Description = "Violation", Type = ViolationType.Ticket,
                Status = TicketStatus.Pending, PenaltyType = penalty, Version = 7,
                UpdatedAt = DateTime.UnixEpoch
            };
            var repository = Proxy<ITicketRepository>((method, args) =>
            {
                Calls.Add(method.Name);
                if (method.Name == nameof(ITicketRepository.GetTicketByIdAsync))
                    return Task.FromResult<Ticket?>((int)args![0]! == 1 ? Ticket : null);
                if (method.Name == nameof(ITicketRepository.SetOriginalVersion))
                {
                    Assert.Equal(7u, args![1]);
                    return null;
                }
                if (method.Name == nameof(ITicketRepository.SaveChangesAsync))
                {
                    Ticket.Version = 8;
                    return Task.CompletedTask;
                }
                throw new InvalidOperationException("Unexpected repository method: " + method.Name);
            });
            var unitOfWork = Proxy<IUnitOfWork>((method, _) => { Calls.Add(method.Name); return Task.CompletedTask; });
            var notification = Proxy<INotificationService>((method, args) =>
            {
                Calls.Add(method.Name);
                if (method.Name == nameof(INotificationService.SendEmailAsync))
                    EmailBodies.Add((string)args![2]!);
                return method.Name == nameof(INotificationService.SendPushNotificationAsync) ? Task.FromResult(true) : Task.CompletedTask;
            });
            var compliance = Proxy<IVendorComplianceScoreService>((method, _) =>
            {
                Calls.Add(method.Name);
                return Task.FromResult(new VendorComplianceScoreResponse
                {
                    Standing = "Good", ViolationFrequencyLevel = "Low", CategorySeverityLevel = "Low"
                });
            });
            Service = new TicketService(repository, null!, null!, unitOfWork, null!, notification, compliance);
        }

        public Task<MarikinaMarket.API.Application.DTOs.Tickets.Internal.UpdateStatusResponse> UpdateAsync(TicketStatus status)
            => Service.UpdateTicketStatusAsync(1, new UpdateStatusRequest { NewStatus = status, Version = 7 });

        public void AssertRejectedWithoutMutation()
        {
            Assert.Equal(TicketStatus.Pending, Ticket.Status);
            Assert.Equal(DateTime.UnixEpoch, Ticket.UpdatedAt);
            Assert.Null(Ticket.ResolvedAt);
            Assert.Equal(7u, Ticket.Version);
            Assert.Equal(["BeginTransactionAsync", "GetTicketByIdAsync", "RollbackAsync"], Calls);
        }
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, TicketStatusProxy>();
        ((TicketStatusProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class TicketStatusProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args);
    }
}
