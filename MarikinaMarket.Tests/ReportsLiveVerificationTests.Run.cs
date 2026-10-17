using System.Diagnostics;
using System.Text.Json;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace MarikinaMarket.Tests;

public partial class ReportsLiveVerificationTests
{
    [ReportsLiveFact]
    public async Task BrowserReportPersistenceAndExports()
    {
        var root = Environment.GetEnvironmentVariable("REPORTS_LIVE_ROOT");
        Assert.False(string.IsNullOrEmpty(root));
        var workspace = Directory.GetParent(Directory.GetParent(root)!.FullName)!.FullName;
        var source = Path.Combine(workspace, "marikina-market-api", "MarikinaMarket.API");
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(source, "appsettings.json"))
            .AddJsonFile(Path.Combine(source, "appsettings.Development.json"), true)
            .AddUserSecrets("bfa5ed50-9a8a-49ec-954c-52201336ac24").Build();
        var connection = new NpgsqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
        Assert.Contains(connection.Host, new[] { "localhost", "127.0.0.1", "::1" });
        connection.Database = "marikina_market_e2e";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection.ConnectionString).UseSnakeCaseNamingConvention().Options;
        await using var db = new AppDbContext(options);
        await db.Database.OpenConnectionAsync();
        Assert.Equal("marikina_market_e2e", db.Database.GetDbConnection().Database);
        Assert.Equal(35, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Empty(await db.Users.ToListAsync());
        Assert.Empty(await db.Tickets.ToListAsync());
        Assert.Empty(await db.Reports.ToListAsync());
        var auditBaseline = await db.AuditLogs.Select(x => x.Id).ToArrayAsync();
        var password = "Report!" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        var marker = "rpt-" + Guid.NewGuid().ToString("N")[..8];
        var users = new List<User>();
        foreach (var role in new[] { "HeadAdmin", "AdminOfficer", "MarketVendor", "MarketEnforcer" })
        {
            var user = new User {
                UserName = marker + role, Email = marker + role + "@example.invalid",
                FirstName = "Report", LastName = role, DateOfBirth = new DateOnly(1990, 1, 1),
                HouseNumber = "1", Street = "Synthetic Street", Barangay = "Test", City = "Marikina",
                Status = AccountStatus.Active, SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(), EmailConfirmed = true
            };
            user.NormalizedUserName = user.UserName.ToUpperInvariant(); user.NormalizedEmail = user.Email.ToUpperInvariant();
            user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password); users.Add(user);
        }
        var mailbox = new Mailbox();
        Microsoft.AspNetCore.Builder.WebApplication? app = null;
        Process? browser = null;
        VendorProfile? vendor = null;
        var ticketIds = new List<int>();
        try
        {
            db.Users.AddRange(users); await db.SaveChangesAsync();
            var roleNames = new[] { "HeadAdmin", "AdminOfficer", "MarketVendor", "MarketEnforcer" };
            for (var i = 0; i < users.Count; i++) {
                var roleId = await db.Roles.Where(r => r.Name == roleNames[i]).Select(r => r.Id).SingleAsync();
                db.UserRoles.Add(new IdentityUserRole<int> { UserId = users[i].Id, RoleId = roleId });
            }
            var sectionId = await db.MarketSections.Select(x => x.Id).FirstAsync();
            vendor = new VendorProfile { UserId = users[2].Id, MarketSectionId = sectionId, BusinessId = marker,
                BusinessName = "Synthetic report fixture", QrCodeValue = marker, RegisteredAt = DateTime.UtcNow, ScoreUpdatedAt = DateTime.UtcNow };
            db.VendorProfiles.Add(vendor); await db.SaveChangesAsync();
            var start = new DateTime(2026, 9, 30, 16, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 10, 2, 16, 0, 0, DateTimeKind.Utc);
            Ticket Add(ViolationType type, TicketStatus? status, decimal? amount, DateTime time) {
                var t = new Ticket { VendorId = vendor.Id, EnforcerId = users[3].Id, MarketSectionId = sectionId,
                    Description = marker, Type = type, Status = status, TotalPaymentAmount = amount, IssuedAt = time, UpdatedAt = time };
                db.Tickets.Add(t); return t;
            }
            Add(ViolationType.Warning, null, null, start);
            Add(ViolationType.Warning, null, null, start.AddHours(2));
            Add(ViolationType.Warning, null, null, end.AddTicks(-10));
            Add(ViolationType.Ticket, TicketStatus.Paid, 300, start.AddHours(3));
            Add(ViolationType.Ticket, TicketStatus.Cleared, 400, start.AddHours(4));
            var changed = Add(ViolationType.Ticket, TicketStatus.Pending, 200, start.AddHours(5));
            Add(ViolationType.Ticket, TicketStatus.Contested, 300, start.AddHours(6));
            Add(ViolationType.Ticket, TicketStatus.Paid, 9000, start.AddTicks(-10));
            Add(ViolationType.Ticket, TicketStatus.Pending, 8000, end);
            await db.SaveChangesAsync(); ticketIds = await db.Tickets.Where(x => x.Description == marker).Select(x => x.Id).ToListAsync();
            await File.WriteAllTextAsync(Path.Combine(root, "fixture.json"), JsonSerializer.Serialize(new {
                database = connection.Database, migrations = 35, from = "2026-10-01", to = "2026-10-02",
                utcStartInclusive = start, utcEndExclusive = end, warnings = 3, resolved = 2, resolvedPenalties = 700,
                pending = 2, pendingPenalties = 500, total = 7, penalties = 1200,
                afterMutation = new { resolved = 3, resolvedPenalties = 900, pending = 1, pendingPenalties = 300 }
            }));
            app = await StartHost(source, connection.ConnectionString, mailbox);
            var info = new ProcessStartInfo("node") { WorkingDirectory = Path.Combine(workspace, "marikina-market-web"),
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            info.ArgumentList.Add("src/features/reports/reports.live-check.mjs");
            info.Environment["REPORTS_LIVE_ROOT"] = root;
            info.Environment["REPORTS_TEST_PASSWORD"] = password;
            info.Environment["REPORTS_TEST_USERS"] = JsonSerializer.Serialize(users.Select(x => new { username = x.UserName, email = x.Email }));
            browser = Process.Start(info)!;
            var errors = browser.StandardError.ReadToEndAsync();
            while (await browser.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromMinutes(3)) is { } line)
            {
                if (!line.StartsWith("RPC:")) continue;
                var request = JsonDocument.Parse(line[4..]).RootElement;
                var action = request.GetProperty("action").GetString();
                object response;
                if (action == "otp") {
                    var index = request.GetProperty("index").GetInt32();
                    response = new { code = mailbox.Codes[users[index].Email!] };
                } else if (action == "mutate") {
                    db.ChangeTracker.Clear(); var ticket = await db.Tickets.SingleAsync(t => t.Id == changed.Id);
                    ticket.Status = TicketStatus.Paid; await db.SaveChangesAsync(); response = new { changed = true };
                } else if (action == "denialTokens") {
                    using var scope = app.Services.CreateScope();
                    var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
                    response = new { vendor = await tokens.GenerateAccessToken(users[2]), enforcer = await tokens.GenerateAccessToken(users[3]) };
                } else throw new InvalidOperationException("Unexpected report verification protocol.");
                await browser.StandardInput.WriteLineAsync(JsonSerializer.Serialize(response)); await browser.StandardInput.FlushAsync();
            }
            await browser.WaitForExitAsync();
            await File.WriteAllTextAsync(Path.Combine(root, "browser-stderr.log"), await errors);
            Assert.Equal(0, browser.ExitCode);
            db.ChangeTracker.Clear();
            var stored = await db.Reports.OrderBy(x => x.Id).ToListAsync();
            Assert.Equal(2, stored.Count);
            Assert.Equal(2, stored[0].ResolvedTicketRecords); Assert.Equal(700m, stored[0].ResolvedTicketPenalties);
            Assert.Equal(3, stored[1].ResolvedTicketRecords); Assert.Equal(900m, stored[1].ResolvedTicketPenalties);
            await File.WriteAllTextAsync(Path.Combine(root, "persistence.json"), JsonSerializer.Serialize(new {
                original = new { stored[0].Id, stored[0].ResolvedTicketRecords, stored[0].ResolvedTicketPenalties, stored[0].PendingTicketRecords },
                updated = new { stored[1].Id, stored[1].ResolvedTicketRecords, stored[1].ResolvedTicketPenalties, stored[1].PendingTicketRecords },
                checkedWithIndependentDbContext = true
            }));
        }
        finally
        {
            if (browser is { HasExited: false }) { browser.Kill(true); await browser.WaitForExitAsync(); }
            browser?.Dispose();
            if (app != null) { await app.StopAsync(); await app.DisposeAsync(); }
            db.ChangeTracker.Clear();
            var ids = users.Select(x => x.Id).Where(x => x > 0).ToArray();
            await db.Reports.Where(x => ids.Contains(x.GeneratedById)).ExecuteDeleteAsync();
            await db.Tickets.Where(x => x.Description == marker).ExecuteDeleteAsync();
            if (vendor is { Id: > 0 }) await db.VendorProfiles.Where(x => x.Id == vendor.Id).ExecuteDeleteAsync();
            await db.OtpVerifications.Where(x => ids.Contains(x.UserId)).ExecuteDeleteAsync();
            await db.RefreshTokens.Where(x => ids.Contains(x.UserId)).ExecuteDeleteAsync();
            await db.UserDeviceTokens.Where(x => ids.Contains(x.UserId)).ExecuteDeleteAsync();
            await db.UserRoles.Where(x => ids.Contains(x.UserId)).ExecuteDeleteAsync();
            await db.AuditLogs.Where(x => !auditBaseline.Contains(x.Id)).ExecuteDeleteAsync();
            await db.Users.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync();
            await File.WriteAllTextAsync(Path.Combine(root, "cleanup.json"), JsonSerializer.Serialize(new {
                database = "marikina_market_e2e", remainingUsers = await db.Users.CountAsync(), remainingTickets = await db.Tickets.CountAsync(),
                remainingReports = await db.Reports.CountAsync(), auditBaselinePreserved = await db.AuditLogs.CountAsync() == auditBaseline.Length,
                hostStopped = true, browserStopped = true
            }));
        }
    }
}

public sealed class ReportsLiveFactAttribute : FactAttribute
{
    public ReportsLiveFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("REPORTS_LIVE_ROOT")))
            Skip = "Set REPORTS_LIVE_ROOT explicitly to run against the disposable local database.";
    }
}
