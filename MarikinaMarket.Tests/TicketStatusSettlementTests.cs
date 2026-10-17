using System.Net;
using System.Globalization;
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
    [InlineData("2026-10-15T15:59:59Z", "2026-10-15", true)]
    [InlineData("2026-10-15T15:59:59Z", "2026-10-16", false)]
    [InlineData("2026-10-15T16:00:00Z", "2026-10-16", true)]
    [InlineData("2026-10-15T16:00:00Z", "2026-10-17", false)]
    [InlineData("2026-10-15T23:59:59Z", "2026-10-16", true)]
    [InlineData("2026-10-15T23:59:59Z", "2026-10-17", false)]
    [InlineData("2026-10-16T00:00:00Z", "2026-10-16", true)]
    [InlineData("2026-10-16T00:00:00Z", "2026-10-17", false)]
    public void Service_date_uses_the_Manila_calendar_day(string utcTime, string serviceDate, bool allowed)
    {
        var validator = typeof(TicketService).GetMethod("ValidateCommunityServiceLog",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(validator);
        var ticket = new Ticket
        {
            Description = "Community service", CommunityServiceHours = 8
        };
        var request = new SubmitCommunityServiceLogRequest
        {
            ServiceDate = DateTime.ParseExact(serviceDate, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            HoursWorked = 1m
        };
        var now = DateTime.Parse(utcTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var error = Record.Exception(() => validator.Invoke(null, [ticket, request, now]));
        if (allowed)
            Assert.Null(error);
        else
        {
            var invocation = Assert.IsType<TargetInvocationException>(error);
            var validation = Assert.IsType<InvalidRequestException>(invocation.InnerException);
            Assert.Equal("Service date cannot be in the future.", validation.Message);
        }
    }

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

    [Theory]
    [InlineData("HeadAdmin")]
    [InlineData("AdminOfficer")]
    public async Task Status_endpoint_returns_400_with_settlement_message_and_200_with_snake_case_json(string role)
    {
        var fixture = new Fixture(PenaltyType.CashFine);
        await using var host = await AuditHost.CreateAsync(services => services.AddSingleton<ITicketService>(fixture.Service));
        const string body = "{\"new_status\":\"Paid\",\"version\":7}";
        var rejected = await host.SendAsync(HttpMethod.Patch, "/api/admin/tickets/1/update-status", role, body);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var error = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
        Assert.Equal("Settlement proof is required before marking this ticket Paid or Cleared.", error.RootElement.GetProperty("message").GetString());
        fixture.AssertRejectedWithoutMutation();

        fixture.Ticket.ProofUrls = ["receipt.png"];
        var accepted = await host.SendAsync(HttpMethod.Patch, "/api/admin/tickets/1/update-status", role, body);
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
    [InlineData("MarketVendor", 1, "Paid", HttpStatusCode.Forbidden)]
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
    public async Task Stale_status_update_returns_conflict_before_mutating_the_ticket()
    {
        var fixture = new Fixture(PenaltyType.CashFine);
        fixture.Ticket.ProofUrls = ["receipt.png"];
        await using var host = await AuditHost.CreateAsync(services => services.AddSingleton<ITicketService>(fixture.Service));
        var response = await host.SendAsync(HttpMethod.Patch, "/api/admin/tickets/1/update-status",
            body: "{\"new_status\":\"Paid\",\"version\":6}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("This ticket was updated by another user. Please refresh and try again.",
            error.RootElement.GetProperty("message").GetString());
        fixture.AssertRejectedWithoutMutation();
    }

    [Theory]
    [InlineData(nameof(INotificationService.SendEmailAsync))]
    [InlineData(nameof(INotificationService.SendPushNotificationAsync))]
    public async Task Delivery_failure_after_commit_does_not_report_a_saved_payment_as_failed(string failedMethod)
    {
        var fixture = new Fixture(PenaltyType.CashFine, failedMethod);
        fixture.Ticket.ProofUrls = ["receipt.png"];
        fixture.Ticket.Vendor = new VendorProfile
        {
            BusinessId = "business", BusinessName = "Stall", QrCodeValue = "qr",
            User = new User
            {
                FirstName = "Juan", LastName = "Reyes", Email = "vendor@example.invalid",
                DateOfBirth = new DateOnly(1990, 1, 1), HouseNumber = "1", Street = "Street",
                Barangay = "Barangay", City = "Marikina"
            }
        };
        var response = await fixture.UpdateAsync(TicketStatus.Paid);
        Assert.Equal(TicketStatus.Paid, response.Status);
        Assert.Equal(8u, response.Version);
        Assert.NotNull(fixture.Ticket.ResolvedAt);
        Assert.Contains("CommitAsync", fixture.Calls);
        Assert.Contains("SaveNotificationAsync", fixture.Calls);
        Assert.True(fixture.Calls.IndexOf(failedMethod) > fixture.Calls.IndexOf("CommitAsync"));
        Assert.Contains(nameof(INotificationService.SendPushNotificationAsync), fixture.Calls);
        Assert.DoesNotContain("RollbackAsync", fixture.Calls);
    }

    [Fact]
    public async Task Another_enforcer_cannot_view_the_saved_receipt()
    {
        await using var host = await VendorTicketHost.CreateAsync(configureTickets: tickets =>
        {
            tickets[0].PenaltyType = PenaltyType.CashFine;
            tickets[0].ProofUrls = ["receipt.png"];
        });
        using var response = await host.GetAsync("/api/enforcer/tickets/101/settlement/receipt",
            role: "MarketEnforcer", userId: "99");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, host.Storage.Calls);
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

    [Theory]
    [InlineData(TicketStatus.Paid, "#4C8B5A")]
    [InlineData(TicketStatus.Cleared, "#4C8B5A")]
    [InlineData(TicketStatus.Waived, "#4C8B5A")]
    [InlineData(TicketStatus.Contested, "#B8792F")]
    [InlineData(TicketStatus.Overdue, "#C0392B")]
    [InlineData(TicketStatus.Pending, "#0B2D5B")]
    [InlineData(TicketStatus.InProgress, "#0B2D5B")]
    public async Task Status_email_accent_matches_the_new_status(TicketStatus status, string accent)
    {
        var fixture = new Fixture(PenaltyType.CashFine);
        fixture.Ticket.ProofUrls = ["receipt.png"];
        fixture.Ticket.Vendor = new VendorProfile
        {
            BusinessId = "business", BusinessName = "Stall", QrCodeValue = "qr",
            User = new User
            {
                FirstName = "Juan", LastName = "Reyes", Email = "vendor@example.invalid",
                DateOfBirth = new DateOnly(1990, 1, 1), HouseNumber = "1", Street = "Street",
                Barangay = "Barangay", City = "Marikina"
            }
        };
        await fixture.UpdateAsync(status);
        Assert.Equal(accent, Assert.Single(fixture.EmailAccents));
    }

    private sealed class Fixture
    {
        public Ticket Ticket { get; }
        public TicketService Service { get; }
        public List<string> Calls { get; } = [];
        public List<string> EmailBodies { get; } = [];
        public List<string> EmailAccents { get; } = [];

        public Fixture(PenaltyType? penalty, string? failedNotification = null)
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
                if (method.Name == failedNotification)
                    throw new InvalidOperationException("Simulated delivery failure.");
                if (method.Name == nameof(INotificationService.SendEmailAsync))
                {
                    EmailBodies.Add((string)args![2]!);
                    EmailAccents.Add(args.Length > 3 ? (string)args[3]! : "#0B2D5B");
                }
                return method.Name is nameof(INotificationService.SendPushNotificationAsync)
                    or nameof(INotificationService.SendPushNotificationToVendorAsync) ? Task.FromResult(true) : Task.CompletedTask;
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
