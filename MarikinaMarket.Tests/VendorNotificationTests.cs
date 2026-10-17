using System.Net;
using System.Reflection;
using System.Text.Json;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using MarikinaMarket.API.Infrastructure.Repositories;
using MarikinaMarket.API.Presentation.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MarikinaMarket.Tests;

public class VendorNotificationTests
{
    [Fact]
    public void Migration_matches_model_and_only_changes_notifications_with_an_owned_backfill()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused").UseSnakeCaseNamingConvention().Options);
        Assert.False(context.Database.HasPendingModelChanges());
        const string id = "20261014120000_AddVendorNotifications";
        var migrations = context.GetService<IMigrationsAssembly>();
        var migration = migrations.CreateMigration(migrations.Migrations[id], context.Database.ProviderName!);
        var tables = migration.UpOperations.Select(operation => operation.GetType().GetProperty("Table")?.GetValue(operation))
            .OfType<string>().ToList();
        Assert.NotEmpty(tables);
        Assert.All(tables, table => Assert.Equal("notifications", table));
        Assert.DoesNotContain(migration.UpOperations, operation => operation is DropTableOperation or DeleteDataOperation
            or InsertDataOperation or UpdateDataOperation);
        var sql = context.GetService<IMigrator>().GenerateScript("20261013080207_UpdateOrdinancesAndPenalties", id);
        Assert.Contains("vendor_is_read", sql);
        Assert.Contains("SET vendor_id = ticket.vendor_id", sql);
        Assert.Contains("FOREIGN KEY (vendor_id)", sql);
        Assert.Contains("ON DELETE RESTRICT", sql);
        Assert.Contains("created_at DESC, id DESC", sql);
    }

    [Fact]
    public void Model_adds_required_vendor_ownership_and_independent_read_state()
    {
        using var fixture = new Fixture();
        var model = fixture.Model.FindEntityType(typeof(Notification))!;
        Assert.NotNull(model.FindProperty("VendorId"));
        Assert.False(model.FindProperty("VendorId")!.IsNullable);
        Assert.NotNull(model.FindProperty("VendorIsRead"));
        Assert.Contains(model.GetForeignKeys(), key => key.Properties.Any(property => property.Name == "VendorId")
            && key.PrincipalEntityType.ClrType == typeof(VendorProfile));
        Assert.Contains(model.GetIndexes(), index => index.Properties[0].Name == "VendorId");
    }

    [Fact]
    public async Task Vendor_list_is_owned_paged_and_independent_of_enforcer_read_state()
    {
        using var fixture = new Fixture();
        for (var id = 1; id <= 12; id++) fixture.Add(id, 17, id == 1);
        fixture.Add(99, 18);
        await using var host = await fixture.HostAsync();
        using var response = await host.SendAsync(HttpMethod.Get, "/api/Notification/vendor?filter=Unread", "MarketVendor");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(11, json.RootElement.GetProperty("total").GetInt32());
        Assert.True(json.RootElement.GetProperty("has_more").GetBoolean());
        var rows = json.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(10, rows.Count);
        Assert.Equal(12, rows[0].GetProperty("id").GetInt32());
        Assert.All(rows, row =>
        {
            Assert.Equal(17, row.GetProperty("vendor_id").GetInt32());
            Assert.False(row.GetProperty("is_read").GetBoolean());
        });
        using var next = await host.SendAsync(HttpMethod.Get, "/api/Notification/vendor?offset=10", "MarketVendor");
        using var page = JsonDocument.Parse(await next.Content.ReadAsStringAsync());
        Assert.False(page.RootElement.GetProperty("has_more").GetBoolean());
        Assert.Equal(2, page.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Warning_metadata_has_no_penalty_status_or_due_date()
    {
        using var fixture = new Fixture();
        var record = fixture.Add(71, 17);
        record.Ticket!.Type = ViolationType.Warning;
        record.Ticket.Status = null;
        record.Ticket.PenaltyType = null;
        await using var host = await fixture.HostAsync();
        using var response = await host.SendAsync(HttpMethod.Get, "/api/Notification/vendor", "MarketVendor");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = json.RootElement.GetProperty("items")[0];
        Assert.Equal("Warning", row.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("penalty_type").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("status").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("due_date").ValueKind);
    }

    [Fact]
    public async Task Vendor_read_changes_only_vendor_flag_and_rejects_other_owners()
    {
        using var fixture = new Fixture();
        var own = fixture.Add(71, 17);
        own.IsRead = false;
        fixture.Add(72, 18);
        await using var host = await fixture.HostAsync();
        using var response = await host.SendAsync(HttpMethod.Patch, "/api/Notification/vendor/71/read", "MarketVendor");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(own.IsRead);
        Assert.True(own.VendorIsRead);
        using var other = await host.SendAsync(HttpMethod.Patch, "/api/Notification/vendor/72/read", "MarketVendor");
        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        await fixture.Service.MarkAsReadNotificationAsync(71, 8);
        Assert.True(own.IsRead);
        Assert.True(own.VendorIsRead);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("MarketEnforcer", 403)]
    [InlineData("AdminOfficer", 403)]
    [InlineData("HeadAdmin", 403)]
    public async Task Vendor_routes_require_a_vendor_identity(string? role, int status)
    {
        using var fixture = new Fixture();
        await using var host = await fixture.HostAsync();
        using var response = await host.SendAsync(HttpMethod.Get, "/api/Notification/vendor", role);
        Assert.Equal(status, (int)response.StatusCode);
        using var read = await host.SendAsync(HttpMethod.Patch, "/api/Notification/vendor/1/read", role);
        Assert.Equal(status, (int)read.StatusCode);
    }

    [Theory]
    [InlineData("offset=-1")]
    [InlineData("filter=Unknown")]
    public async Task Vendor_query_validation_rejects_invalid_parameters(string query)
    {
        using var fixture = new Fixture();
        await using var host = await fixture.HostAsync();
        using var response = await host.SendAsync(HttpMethod.Get, "/api/Notification/vendor?" + query, "MarketVendor");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Vendor_push_resolves_profile_id_to_account_tokens()
    {
        using var fixture = new Fixture();
        var result = await fixture.Service.SendPushNotificationToVendorAsync(17, "New inspection", "A warning was issued.",
            new Dictionary<string, string> { ["ticketId"] = "11" });
        Assert.True(result);
        Assert.Equal(7, fixture.TokenOwner);
        Assert.Equal(1, fixture.PushCalls);
    }

    [Fact]
    public void Vendor_can_register_a_device_token()
    {
        var roles = typeof(UserController).GetMethod(nameof(UserController.RegisterDeviceToken))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',');
        Assert.Contains("MarketVendor", roles);
    }

    private sealed class Fixture : AppDbContext
    {
        public List<Notification> Records { get; } = [];
        public NotificationService Service { get; }
        public int TokenOwner, PushCalls;
        public Fixture() : base(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused").UseSnakeCaseNamingConvention().Options)
        {
            Notifications = new RegistrationDbSet<Notification>(Records);
            VendorProfiles = new RegistrationDbSet<VendorProfile>(
            [new() { Id = 17, UserId = 7, BusinessId = "MM-17", BusinessName = "Reyes Fresh Fish", QrCodeValue = "sample" },
             new() { Id = 18, UserId = 42, BusinessId = "MM-18", BusinessName = "Another store", QrCodeValue = "sample2" }]);
            var users = ReviewBoundaryProxy.Create<IUserRepository>((method, args) =>
            {
                TokenOwner = (int)args[0]!;
                return Task.FromResult(new List<string> { "sample-device-token" });
            });
            var push = ReviewBoundaryProxy.Create<IPushNotificationService>((method, args) =>
            {
                PushCalls++;
                return Task.FromResult(true);
            });
            Service = new NotificationService(push, null!, users, new NotificationRepository(this), new VendorRepository(this));
        }
        public Notification Add(int id, int vendorId, bool vendorRead = false)
        {
            var record = new Notification
            {
                Id = id, VendorId = vendorId, VendorIsRead = vendorRead, EnforcerId = 8,
                TicketId = id + 100, Message = "Inspection notice", IsRead = true,
                CreatedAt = new DateTime(2026, 10, 14, 2, 0, 0, DateTimeKind.Utc), Status = TicketStatus.Pending,
                Ticket = new Ticket
                {
                    Id = id + 100, VendorId = vendorId, Description = "Inspection", ControlNumber = "VIO-0101",
                    Type = ViolationType.Ticket, Status = TicketStatus.Pending, PenaltyType = PenaltyType.CashFine,
                    IssuedAt = new DateTime(2026, 10, 14, 2, 0, 0, DateTimeKind.Utc),
                    Vendor = new VendorProfile { Id = vendorId, BusinessId = "MM-17", BusinessName = "Reyes Fresh Fish", QrCodeValue = "sample" },
                    MarketSection = new MarketSection { Name = "Fish and Seafood", Description = "Fish retail" }
                }
            };
            Records.Add(record);
            return record;
        }
        public Task<AuditHost> HostAsync() => AuditHost.CreateAsync(services => services.AddSingleton<INotificationService>(Service));
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }
}
