using System.Net;
using System.Text.Json;
using MarikinaMarket.API.Domain.Enums;
using Xunit;

namespace MarikinaMarket.Tests;

public class AuditHttpTests
{
    [Theory]
    [InlineData("HeadAdmin")]
    [InlineData("AdminOfficer")]
    public async Task Mutation_records_verified_actor_and_generated_target_without_request_secrets(string role)
    {
        await using var host = await AuditHost.CreateAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/api/audit-probe/mutate", role,
            "{\"name\":\"updated\",\"user_id\":999,\"role\":\"MarketVendor\",\"password\":\"secret-password\"}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var log = Assert.Single(host.Repository.Logs);
        Assert.Equal(7, log.UserId);
        Assert.Equal(Enum.Parse<Role>(role), log.Role);
        Assert.Equal("42", log.TargetId);
        Assert.Equal(LogResult.Success, log.Result);
        Assert.Equal(DateTimeKind.Utc, log.Timestamp.Kind);
        Assert.DoesNotContain("secret-password", log.Details);
        Assert.DoesNotContain("999", log.Details);
    }

    [Theory]
    [InlineData("GET", "/api/audit-probe/read")]
    [InlineData("POST", "/api/audit-probe/read-only")]
    [InlineData("GET", "/api/admin/audit-logs")]
    [InlineData("GET", "/api/admin/audit-logs/counts")]
    public async Task Successful_reads_do_not_create_audit_records(string method, string route)
    {
        await using var host = await AuditHost.CreateAsync();
        var response = await host.SendAsync(new HttpMethod(method), route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(host.Repository.Logs);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("MarketVendor", 403)]
    [InlineData("MarketEnforcer", 403)]
    public async Task Authorization_failures_are_logged_even_on_read_endpoints(string? role, int status)
    {
        await using var host = await AuditHost.CreateAsync();
        var response = await host.SendAsync(HttpMethod.Get, "/api/admin/audit-logs", role);
        Assert.Equal(status, (int)response.StatusCode);
        var log = Assert.Single(host.Repository.Logs);
        Assert.Equal("AuthorizationFailed", log.Action);
        Assert.Equal(Module.Security, log.Module);
        Assert.Equal(LogResult.Failed, log.Result);
        Assert.Equal(role is null ? null : 7, log.UserId);
        Assert.Contains("was denied", log.Details);
    }

    [Theory]
    [InlineData("/api/audit-probe/mutate", "{\"name\":\"\"}")]
    [InlineData("/api/audit-probe/mutate", "not-json")]
    [InlineData("/api/audit-probe/validation-exception", null)]
    public async Task Validation_failures_are_excluded(string route, string? body)
    {
        await using var host = await AuditHost.CreateAsync();
        var response = await host.SendAsync(HttpMethod.Post, route, body: body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(host.Repository.Logs);
    }

    [Theory]
    [InlineData("business-failure", 400)]
    [InlineData("business-failure-200", 200)]
    [InlineData("password-failure", 400)]
    [InlineData("unexpected-error", 500)]
    public async Task Business_failures_are_logged_without_exception_or_response_secrets(string action, int status)
    {
        await using var host = await AuditHost.CreateAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/api/audit-probe/" + action);
        Assert.Equal(status, (int)response.StatusCode);
        var log = Assert.Single(host.Repository.Logs);
        Assert.Equal(LogResult.Failed, log.Result);
        Assert.DoesNotContain("sensitive", JsonSerializer.Serialize(log));
    }

    [Fact]
    public async Task Audit_outage_does_not_change_a_successful_operation_response()
    {
        await using var host = await AuditHost.CreateAsync();
        host.Repository.FailWrites = true;
        var response = await host.SendAsync(HttpMethod.Post, "/api/audit-probe/mutate", body: "{\"name\":\"updated\"}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(host.Repository.Logs);
    }

    [Fact]
    public async Task Empty_success_response_is_logged()
    {
        await using var host = await AuditHost.CreateAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/api/audit-probe/no-content");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(LogResult.Success, Assert.Single(host.Repository.Logs).Result);
    }

    [Fact]
    public async Task Committed_mutation_is_not_logged_twice_by_request_capture()
    {
        await using var host = await AuditHost.CreateAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/api/audit-probe/committed");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("42", Assert.Single(host.Repository.Logs).TargetId);
    }

    [Fact]
    public async Task Failure_after_commit_is_distinguished_from_the_successful_mutation()
    {
        await using var host = await AuditHost.CreateAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/api/audit-probe/after-commit-error");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(2, host.Repository.Logs.Count);
        Assert.Equal(LogResult.Success, host.Repository.Logs[0].Result);
        Assert.Equal("RequestFailedAfterCommit", host.Repository.Logs[1].Action);
        Assert.Equal(LogResult.Failed, host.Repository.Logs[1].Result);
        Assert.DoesNotContain("sensitive", JsonSerializer.Serialize(host.Repository.Logs));
    }

    [Fact]
    public async Task Summary_and_detail_endpoints_return_stored_data_without_recursive_logging()
    {
        await using var host = await AuditHost.CreateAsync();
        await host.SendAsync(HttpMethod.Post, "/api/audit-probe/mutate", body: "{\"name\":\"updated\"}");
        var summary = await host.SendAsync(HttpMethod.Get, "/api/admin/audit-logs?offset=0&module=Reports&result=Success&userId=7");
        using var page = JsonDocument.Parse(await summary.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
        Assert.Equal(1, page.RootElement.GetProperty("total").GetInt32());
        Assert.False(page.RootElement.GetProperty("has_more").GetBoolean());
        var item = page.RootElement.GetProperty("items")[0];
        Assert.Equal("Mutate", item.GetProperty("action").GetString());
        Assert.Equal("Juan", item.GetProperty("first_name").GetString());
        Assert.Equal("Dela Cruz", item.GetProperty("last_name").GetString());
        Assert.False(item.TryGetProperty("details", out _));
        var detail = await host.SendAsync(HttpMethod.Get, "/api/admin/audit-logs/1");
        using var data = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.Equal("42", data.RootElement.GetProperty("target_id").GetString());
        Assert.Equal("Juan", data.RootElement.GetProperty("first_name").GetString());
        Assert.Equal("Dela Cruz", data.RootElement.GetProperty("last_name").GetString());
        Assert.Contains("completed successfully", data.RootElement.GetProperty("details").GetString());
        Assert.Single(host.Repository.Logs);
    }

    [Fact]
    public async Task System_and_deleted_actor_names_are_null_in_summary_and_detail_JSON()
    {
        await using var host = await AuditHost.CreateAsync();
        await host.SendAsync(HttpMethod.Post, "/api/audit-probe/mutate", body: "{\"name\":\"updated\"}");
        ((AuditUserRepositoryStub)(object)host.Users).Users.Remove(7);
        var summary = await host.SendAsync(HttpMethod.Get, "/api/admin/audit-logs");
        using var page = JsonDocument.Parse(await summary.Content.ReadAsStringAsync());
        var item = page.RootElement.GetProperty("items")[0];
        Assert.Equal(7, item.GetProperty("user_id").GetInt32());
        Assert.Equal("HeadAdmin", item.GetProperty("role").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("first_name").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("last_name").ValueKind);
        host.Repository.Logs[0].UserId = null;
        host.Repository.Logs[0].Role = null;
        var detail = await host.SendAsync(HttpMethod.Get, "/api/admin/audit-logs/1");
        using var data = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("user_id").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("first_name").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.RootElement.GetProperty("last_name").ValueKind);
        Assert.Single(host.Repository.Logs);
    }

    [Theory]
    [InlineData("HeadAdmin")]
    [InlineData("AdminOfficer")]
    public async Task Counts_return_all_activity_success_and_security_totals_without_logging_the_read(string role)
    {
        await using var host = await AuditHost.CreateAsync();
        await host.SendAsync(HttpMethod.Post, "/api/audit-probe/mutate", body: "{\"name\":\"updated\"}");
        await host.SendAsync(HttpMethod.Post, "/api/audit-probe/business-failure");
        await host.SendAsync(HttpMethod.Get, "/api/admin/audit-logs", "MarketVendor");
        var response = await host.SendAsync(HttpMethod.Get, "/api/admin/audit-logs/counts", role);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, json.RootElement.GetProperty("recorded_activities").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("successful_actions").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("security_events").GetInt32());
        Assert.Equal(3, host.Repository.Logs.Count);
        var filtered = await host.SendAsync(HttpMethod.Get, "/api/admin/audit-logs/counts?module=Security&result=Failed", role);
        using var selected = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
        Assert.Equal(1, selected.RootElement.GetProperty("recorded_activities").GetInt32());
        Assert.Equal(0, selected.RootElement.GetProperty("successful_actions").GetInt32());
        Assert.Equal(1, selected.RootElement.GetProperty("security_events").GetInt32());
        Assert.Equal(3, host.Repository.Logs.Count);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("MarketVendor", 403)]
    [InlineData("MarketEnforcer", 403)]
    public async Task Counts_enforce_admin_authorization_and_audit_denied_access(string? role, int status)
    {
        await using var host = await AuditHost.CreateAsync();
        var response = await host.SendAsync(HttpMethod.Get, "/api/admin/audit-logs/counts", role);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("AuthorizationFailed", Assert.Single(host.Repository.Logs).Action);
    }

    [Fact]
    public async Task Counts_return_zero_for_an_empty_table()
    {
        await using var host = await AuditHost.CreateAsync();
        var response = await host.SendAsync(HttpMethod.Get, "/api/admin/audit-logs/counts");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, json.RootElement.GetProperty("recorded_activities").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("successful_actions").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("security_events").GetInt32());
        Assert.Empty(host.Repository.Logs);
    }

    [Fact]
    public async Task Table_and_counts_accept_repeated_selections_and_date_presets()
    {
        await using var host = await AuditHost.CreateAsync();
        await host.SendAsync(HttpMethod.Post, "/api/audit-probe/mutate", body: "{\"name\":\"updated\"}");
        await host.SendAsync(HttpMethod.Post, "/api/audit-probe/business-failure");
        await host.SendAsync(HttpMethod.Get, "/api/admin/audit-logs", "MarketVendor");
        const string filters = "modules=Reports&modules=Security&results=Success&results=Failed&dateRange=Last7Days";
        var response = await host.SendAsync(HttpMethod.Get, "/api/admin/audit-logs?offset=0&" + filters);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var page = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3, page.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(3, page.RootElement.GetProperty("items").GetArrayLength());
        Assert.False(page.RootElement.GetProperty("has_more").GetBoolean());
        var counts = await host.SendAsync(HttpMethod.Get, "/api/admin/audit-logs/counts?" + filters);
        Assert.Equal(HttpStatusCode.OK, counts.StatusCode);
        using var data = JsonDocument.Parse(await counts.Content.ReadAsStringAsync());
        Assert.Equal(3, data.RootElement.GetProperty("recorded_activities").GetInt32());
        Assert.Equal(1, data.RootElement.GetProperty("successful_actions").GetInt32());
        Assert.Equal(1, data.RootElement.GetProperty("security_events").GetInt32());
        Assert.Equal(3, host.Repository.Logs.Count);
        var searched = await host.SendAsync(HttpMethod.Get, "/api/admin/audit-logs?" + filters + "&search=denied");
        using var searchPage = JsonDocument.Parse(await searched.Content.ReadAsStringAsync());
        Assert.Equal(1, searchPage.RootElement.GetProperty("total").GetInt32());
    }

    [Theory]
    [InlineData("/api/admin/audit-logs?offset=-1", 400)]
    [InlineData("/api/admin/audit-logs?module=999", 400)]
    [InlineData("/api/admin/audit-logs?fromDate=2026-10-11&toDate=2026-10-10", 400)]
    [InlineData("/api/admin/audit-logs/0", 400)]
    [InlineData("/api/admin/audit-logs/999", 404)]
    [InlineData("/api/admin/audit-logs/counts?module=999", 400)]
    [InlineData("/api/admin/audit-logs/counts?userId=0", 400)]
    [InlineData("/api/admin/audit-logs/counts?fromDate=2026-10-11&toDate=2026-10-10", 400)]
    [InlineData("/api/admin/audit-logs?dateRange=Custom", 400)]
    [InlineData("/api/admin/audit-logs?dateRange=999", 400)]
    [InlineData("/api/admin/audit-logs?modules=Users&modules=999", 400)]
    [InlineData("/api/admin/audit-logs?results=Unknown", 400)]
    [InlineData("/api/admin/audit-logs?module=Users&modules=Security", 400)]
    [InlineData("/api/admin/audit-logs?dateRange=Last7Days&fromDate=2026-10-10", 400)]
    [InlineData("/api/admin/audit-logs/counts?dateRange=Custom", 400)]
    [InlineData("/api/admin/audit-logs/counts?results=Failed&result=Success", 400)]
    public async Task Read_endpoints_validate_filters_and_missing_records(string route, int status)
    {
        await using var host = await AuditHost.CreateAsync();
        var response = await host.SendAsync(HttpMethod.Get, route);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Empty(host.Repository.Logs);
    }
}
