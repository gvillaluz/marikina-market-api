using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using MarikinaMarket.API.Application;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace MarikinaMarket.Tests;

public class FullBackupE2ETests
{
    [FullBackupE2EFact]
    public async Task BrowserSchedulerRestartAndRestore()
    {
        var root = Environment.GetEnvironmentVariable("E2E_FULL_ROOT")!;
        var workspace = Directory.GetParent(Directory.GetParent(root)!.FullName)!.FullName;
        var source = Path.Combine(workspace, "marikina-market-api", "MarikinaMarket.API");
        var config = new ConfigurationBuilder().AddJsonFile(Path.Combine(source, "appsettings.json"))
            .AddJsonFile(Path.Combine(source, "appsettings.Development.json"), true)
            .AddUserSecrets("bfa5ed50-9a8a-49ec-954c-52201336ac24").Build();
        var conn = new NpgsqlConnectionStringBuilder(config.GetConnectionString("DefaultConnection"));
        Assert.Contains(conn.Host, new[] { "localhost", "127.0.0.1", "::1" });
        conn.Database = "marikina_market_e2e";
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "temp")), Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar));
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conn.ConnectionString).UseSnakeCaseNamingConvention().Options;
        await using var db = new AppDbContext(options);
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.Backups.CountAsync());
        Assert.Equal(0, await db.BackupSchedules.CountAsync());
        Assert.Equal(0, await db.AuditLogs.CountAsync());
        var key = RandomNumberGenerator.GetBytes(32);
        var jwt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        var password = "E2e!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(18));
        var user = new User {
            UserName = "e2e-backup-" + Guid.NewGuid().ToString("N")[..10],
            Email = config["Email:FromAddress"], FirstName = "Backup", LastName = "Verification",
            DateOfBirth = new DateOnly(1990, 1, 1), HouseNumber = "1", Street = "Test Street",
            Barangay = "Test Barangay", City = "Marikina", Status = AccountStatus.Active,
            SecurityStamp = Guid.NewGuid().ToString(), ConcurrencyStamp = Guid.NewGuid().ToString(), EmailConfirmed = true
        };
        Assert.False(string.IsNullOrEmpty(user.Email));
        user.NormalizedUserName = user.UserName.ToUpperInvariant(); user.NormalizedEmail = user.Email!.ToUpperInvariant();
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        var results = new Dictionary<string, object>();
        Process? api = null, browser = null;
        Task? browserPump = null;
        var browserReady = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var browserVerified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var restoreCreated = false;
        var restoreConn = new NpgsqlConnectionStringBuilder(conn.ConnectionString) { Database = "marikina_market_e2e_restore" };
        var maintenance = new NpgsqlConnectionStringBuilder(conn.ConnectionString) { Database = "postgres" };
        var stage = "setup";
        async Task Progress(string value) { stage = value; await File.WriteAllTextAsync(Path.Combine(root, "server-progress.json"), JsonSerializer.Serialize(new { stage, utc = DateTime.UtcNow })); }
        Process Start(string file, IEnumerable<string> args, Dictionary<string, string>? env = null, bool stdin = false, string? cwd = null)
        {
            var info = new ProcessStartInfo(file) { WorkingDirectory = cwd ?? source, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = stdin };
            foreach (var arg in args) info.ArgumentList.Add(arg);
            if (env != null) foreach (var item in env) info.Environment[item.Key] = item.Value;
            return Process.Start(info)!;
        }
        async Task Stop(Process? process)
        {
            if (process == null) return;
            if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); }
            process.Dispose();
        }
        async Task<Process> StartApi(bool brokenDump = false)
        {
            var process = Start("dotnet", new[] { Path.Combine(source, "bin", "E2EFullVerification", "net10.0", "MarikinaMarket.API.dll") }, new() {
                ["ASPNETCORE_ENVIRONMENT"] = "Development", ["DOTNET_ENVIRONMENT"] = "Development",
                ["ConnectionStrings__DefaultConnection"] = conn.ConnectionString,
                ["Jwt__Secret"] = jwt, ["Jwt__Issuer"] = "local-e2e", ["Jwt__Audience"] = "local-e2e",
                ["Backups__EncryptionKey"] = Convert.ToBase64String(key),
                ["Backups__PgDumpPath"] = brokenDump ? Path.Combine(root, "missing-pg-dump.exe") : @"D:\PostgreSQL\Data\bin\pg_dump.exe",
                ["Cors__AllowedOrigins__0"] = "http://localhost:5176", ["Logging__LogLevel__Default"] = "Warning"
            });
            _ = process.StandardOutput.ReadToEndAsync(); _ = process.StandardError.ReadToEndAsync();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            for (var i = 0; i < 60; i++)
            {
                if (process.HasExited) throw new InvalidOperationException("Local API exited before readiness.");
                try { if ((await http.GetAsync("http://127.0.0.1:5076/api/admin/backups")).StatusCode == HttpStatusCode.Unauthorized) return process; } catch (HttpRequestException) { } catch (TaskCanceledException) { }
                await Task.Delay(1000);
            }
            await Stop(process); throw new TimeoutException("Local API did not become ready.");
        }
        async Task WaitFor(Func<Task<bool>> predicate, TimeSpan timeout)
        {
            var until = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < until) { db.ChangeTracker.Clear(); if (await predicate()) return; await Task.Delay(2000); }
            throw new TimeoutException("Expected database state was not reached during " + stage);
        }
        async Task SqlMaintenance(string sql)
        {
            await using var connection = new NpgsqlConnection(maintenance.ConnectionString); await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync();
        }
        try
        {
            db.Users.Add(user); db.BackupSchedules.Add(new BackupSchedule { Enabled = false, UpdatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            db.UserRoles.Add(new IdentityUserRole<int> { UserId = user.Id, RoleId = 4 }); await db.SaveChangesAsync();
            api = await StartApi(); results["anonymousHistory"] = 401;
            await Progress("browser_login");
            browser = Start("node", new[] { Path.Combine(root, "browser.mjs") }, new() {
                ["E2E_ROOT"] = root, ["E2E_WEB"] = Path.Combine(workspace, "marikina-market-web"),
                ["E2E_USERNAME"] = user.UserName, ["E2E_PASSWORD"] = password
            }, true);
            _ = browser.StandardError.ReadToEndAsync();
            browserPump = Task.Run(async () => {
                string? token = null;
                while (await browser.StandardOutput.ReadLineAsync() is { } line) {
                    if (line.StartsWith("PRIVATE_TOKEN:")) token = line[14..];
                    else if (line == "BROWSER_READY") browserReady.TrySetResult(token!);
                    else if (line == "BROWSER_VERIFIED") browserVerified.TrySetResult();
                    else if (line.StartsWith("BROWSER_FAILED:")) { browserReady.TrySetException(new InvalidOperationException(line)); browserVerified.TrySetException(new InvalidOperationException(line)); }
                }
                if (!browserReady.Task.IsCompleted) browserReady.TrySetException(new InvalidOperationException("Browser exited before verification."));
            });
            string token;
            try { token = await browserReady.Task.WaitAsync(TimeSpan.FromMinutes(13)); }
            catch {
                db.ChangeTracker.Clear();
                var stored = await db.Users.SingleAsync();
                await File.WriteAllTextAsync(Path.Combine(root, "account-check.json"), JsonSerializer.Serialize(new {
                    passwordHashValid = new PasswordHasher<User>().VerifyHashedPassword(stored, stored.PasswordHash!, password) != PasswordVerificationResult.Failed,
                    normalizedNameValid = stored.NormalizedUserName == stored.UserName!.ToUpperInvariant(),
                    localAuditCount = await db.AuditLogs.CountAsync(), localOtpCount = await db.OtpVerifications.CountAsync(), stored.AccessFailedCount
                })); throw;
            }
            Assert.False(string.IsNullOrEmpty(token));
            db.ChangeTracker.Clear();
            var manual = await db.Backups.SingleAsync(); Assert.Equal(BackupStatus.Completed, manual.Status);
            var schedule = await db.BackupSchedules.SingleAsync(); Assert.False(schedule.Enabled); Assert.Equal(new TimeOnly(23, 59), schedule.Time);
            results["realEmailOtpBrowserLogin"] = "passed"; results["browserSaveManualBackupDownload"] = "passed";
            var encrypted = await File.ReadAllBytesAsync(Path.Combine(root, "temp", "browser-download.mmbak"));
            Assert.Equal(new byte[] { 77, 77, 66, 75, 1 }, encrypted.Take(5)); Assert.Equal(manual.Size, encrypted.LongLength);
            await Stop(api); api = null;
            await Progress("automatic_failure_and_recovery");
            db.ChangeTracker.Clear(); schedule = await db.BackupSchedules.SingleAsync();
            schedule.Enabled = true; schedule.UpdatedAt = DateTime.UtcNow.AddDays(-2);
            schedule.Time = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(8).AddMinutes(-1));
            var interrupted = new Backup { B2Key = "e2e-restart/" + Guid.NewGuid().ToString("N"), DateTime = DateTime.UtcNow.AddHours(-1), Type = BackupType.Manual, Status = BackupStatus.Failed };
            db.Backups.Add(interrupted); await db.SaveChangesAsync(); var interruptedId = interrupted.Id;
            api = await StartApi(true);
            await WaitFor(async () => await db.Backups.AnyAsync(x => x.Type == BackupType.Automatic && x.IsFinalized && x.Status == BackupStatus.Failed), TimeSpan.FromMinutes(1));
            db.ChangeTracker.Clear(); schedule = await db.BackupSchedules.AsNoTracking().SingleAsync();
            var retry = schedule.NextRetryAt!.Value; var occurrence = schedule.LastAttemptScheduledAt;
            Assert.True(await db.Backups.AnyAsync(x => x.Id == interruptedId && x.IsFinalized && x.Status == BackupStatus.Failed));
            Assert.InRange((retry - DateTime.UtcNow).TotalSeconds, 230, 301);
            results["interruptedHistoryRecovered"] = true; results["automaticFailureRetryDueUtc"] = retry;
            var beforeRestartCount = await db.Backups.CountAsync();
            await Stop(api); api = null; api = await StartApi();
            db.ChangeTracker.Clear(); Assert.Equal(retry, (await db.BackupSchedules.AsNoTracking().SingleAsync()).NextRetryAt);
            Assert.Equal(beforeRestartCount, await db.Backups.CountAsync());
            await Progress("waiting_for_real_five_minute_retry");
            await WaitFor(async () => await db.Backups.AnyAsync(x => x.Type == BackupType.Automatic && x.Status == BackupStatus.Completed), TimeSpan.FromMinutes(6));
            db.ChangeTracker.Clear(); schedule = await db.BackupSchedules.SingleAsync();
            Assert.Null(schedule.NextRetryAt); Assert.Equal(occurrence, schedule.LastAttemptScheduledAt);
            Assert.Equal(2, await db.Backups.CountAsync(x => x.Type == BackupType.Automatic));
            await Task.Delay(31000); Assert.Equal(2, await db.Backups.CountAsync(x => x.Type == BackupType.Automatic));
            schedule.Enabled = false; await db.SaveChangesAsync();
            results["persistedRetrySurvivedRestartAndSucceeded"] = true; results["noDuplicateOccurrenceAfterSuccess"] = true;
            await browser.StandardInput.WriteLineAsync("VERIFY"); await browser.StandardInput.FlushAsync();
            await browserVerified.Task.WaitAsync(TimeSpan.FromMinutes(1));
            results["browserPersistedHistoryAfterApiRestart"] = "passed";
            await Stop(browser); browser = null;
            await Stop(api); api = null;
            await Progress("restore_drill");
            var plaintext = new byte[encrypted.Length - 33];
            using (var aes = new AesGcm(key, 16)) aes.Decrypt(encrypted.AsSpan(5, 12), encrypted.AsSpan(33), encrypted.AsSpan(17, 16), plaintext, encrypted.AsSpan(0, 5));
            Assert.Equal("PGDMP", System.Text.Encoding.ASCII.GetString(plaintext, 0, 5));
            var dump = Path.Combine(root, "temp", "restore.dump");
            try {
                await File.WriteAllBytesAsync(dump, plaintext);
                // CREATE fails if this name already exists. Never overwrite a preexisting database.
                await SqlMaintenance("CREATE DATABASE marikina_market_e2e_restore"); restoreCreated = true;
                var restore = Start(@"D:\PostgreSQL\Data\bin\pg_restore.exe", new[] { "--exit-on-error", "--no-owner", "--no-privileges", "--host", conn.Host!, "--port", conn.Port.ToString(), "--username", conn.Username!, "--dbname", restoreConn.Database!, dump }, new() { ["PGPASSWORD"] = conn.Password!, ["PGCONNECT_TIMEOUT"] = "5" });
                var stdout = restore.StandardOutput.ReadToEndAsync(); var stderr = restore.StandardError.ReadToEndAsync();
                await restore.WaitForExitAsync(); await stdout; await stderr; Assert.Equal(0, restore.ExitCode); restore.Dispose();
                await using (var restored = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(restoreConn.ConnectionString).UseSnakeCaseNamingConvention().Options)) {
                    Assert.Equal(35, (await restored.Database.GetAppliedMigrationsAsync()).Count());
                    Assert.Equal(4, await restored.Roles.CountAsync()); Assert.Equal(1, await restored.Users.CountAsync());
                    Assert.Equal(user.UserName, (await restored.Users.SingleAsync()).UserName);
                    Assert.Equal(await db.MarketSections.CountAsync(), await restored.MarketSections.CountAsync());
                    Assert.Equal(await db.Ordinances.CountAsync(), await restored.Ordinances.CountAsync());
                    Assert.Equal(1, await restored.Backups.CountAsync());
                }
                results["browserDownloadedBackupDecryptedAndRestored"] = "passed"; results["restoredMigrationCount"] = 35;
            } finally { CryptographicOperations.ZeroMemory(plaintext); if (File.Exists(dump)) File.Delete(dump); }
        }
        catch (Exception ex) { results["failedStage"] = stage; results["failureType"] = ex.GetType().Name; throw; }
        finally
        {
            await Stop(browser); await Stop(api);
            if (restoreCreated) { NpgsqlConnection.ClearAllPools(); await SqlMaintenance("DROP DATABASE marikina_market_e2e_restore WITH (FORCE)"); results["restoreDatabaseRemoved"] = true; }
            db.ChangeTracker.Clear(); var owned = await db.Backups.ToListAsync();
            await File.WriteAllTextAsync(Path.Combine(root, "owned-object-keys.json"), JsonSerializer.Serialize(owned.Select(x => x.B2Key)));
            var storage = new StorageService(config);
            foreach (var row in owned) {
                await storage.DeleteAllVersionsAsync(B2BucketType.Backups, row.B2Key, default);
                await Assert.ThrowsAsync<RecordNotFoundException>(() => storage.DownloadStreamAsync(B2BucketType.Backups, row.B2Key, default));
            }
            // All these tables were empty before this exclusive disposable-database run.
            await db.AuditLogs.ExecuteDeleteAsync(); await db.OtpVerifications.ExecuteDeleteAsync(); await db.RefreshTokens.ExecuteDeleteAsync();
            await db.UserDeviceTokens.ExecuteDeleteAsync(); await db.UserRoles.ExecuteDeleteAsync();
            await db.Backups.ExecuteDeleteAsync(); await db.BackupSchedules.ExecuteDeleteAsync(); await db.Users.ExecuteDeleteAsync();
            results["ownedObjectKeysRemovedAnd404"] = owned.Count;
            results["remainingBackups"] = await db.Backups.CountAsync(); results["remainingSchedules"] = await db.BackupSchedules.CountAsync(); results["remainingUsers"] = await db.Users.CountAsync();
            await File.WriteAllTextAsync(Path.Combine(root, "result.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            CryptographicOperations.ZeroMemory(key);
        }
    }
}

public sealed class FullBackupE2EFactAttribute : FactAttribute
{
    public FullBackupE2EFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("E2E_FULL_ROOT")))
            Skip = "Set E2E_FULL_ROOT explicitly to run the disposable local backup and restore drill.";
    }
}

