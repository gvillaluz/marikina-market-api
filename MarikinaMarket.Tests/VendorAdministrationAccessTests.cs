using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Presentation.Controllers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace MarikinaMarket.Tests;

public class VendorAdministrationAccessTests
{
    [Theory]
    [InlineData("HeadAdmin", 200)]
    [InlineData("AdminOfficer", 200)]
    [InlineData("MarketEnforcer", 403)]
    [InlineData("MarketVendor", 403)]
    [InlineData(null, 401)]
    public async Task Vendor_read_routes_enforce_admin_roles(string? role, int expectedStatus)
    {
        await using var host = await VendorAdministrationAccessHost.Create();
        foreach (var path in VendorAdministrationAccessHost.Paths)
        {
            using var response = await host.Send(path, role);
            Assert.Equal(expectedStatus, (int)response.StatusCode);
        }
        Assert.Equal(expectedStatus == 200 ? 4 : 0, host.ServiceCalls);
    }

    [Fact]
    public async Task Invalid_ids_and_negative_offset_are_rejected_before_service_calls()
    {
        await using var host = await VendorAdministrationAccessHost.Create();
        foreach (var path in new[] { "/api/admin/vendors?offset=-1", "/api/admin/vendors/0/profile", "/api/admin/vendors/-1/compliance-score", "/api/admin/vendors/0/inspections" })
        {
            using var response = await host.Send(path, "HeadAdmin");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        Assert.Equal(0, host.ServiceCalls);
    }
}

// Authorization/model binding are under test; service responses are the boundary.
// No application startup, database, storage, email or push configuration is used.
internal sealed class VendorAdministrationAccessHost : IAsyncDisposable
{
    public static readonly string[] Paths = ["/api/admin/vendors", "/api/admin/vendors/70/profile", "/api/admin/vendors/70/compliance-score", "/api/admin/vendors/70/inspections"];
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly SymmetricSecurityKey _key = new(RandomNumberGenerator.GetBytes(32));
    public int ServiceCalls;

    public static async Task<VendorAdministrationAccessHost> Create()
    {
        var host = new VendorAdministrationAccessHost();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(ReviewBoundaryProxy.Create<IVendorService>((method, _) =>
        {
            host.ServiceCalls++;
            return method.Name switch
            {
                "GetAdminVendorSummariesAsync" => Task.FromResult(new PageResponse<AdminVendorSummaryResponse> { Items = [], Total = 0, HasMore = false }),
                "GetVendorProfileAsync" => Task.FromResult(new VendorProfileResponse { Name = "Juan Reyes" }),
                "GetVendorComplianceScoreAsync" => Task.FromResult(new VendorComplianceScoreResponse { Standing = "Good", ViolationFrequencyLevel = "Low", CategorySeverityLevel = "Low" }),
                _ => throw new InvalidOperationException(method.Name)
            };
        }));
        builder.Services.AddSingleton(ReviewBoundaryProxy.Create<ITicketService>((method, _) =>
        {
            if (method.Name != "GetVendorInspectionHistoryAsync") throw new InvalidOperationException(method.Name);
            host.ServiceCalls++;
            return Task.FromResult(new PageResponse<VendorInspectionHistoryResponse> { Items = [], Total = 0, HasMore = false });
        }));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = "vendor-admin-tests",
                ValidateAudience = true, ValidAudience = "vendor-admin-tests",
                ValidateIssuerSigningKey = true, IssuerSigningKey = host._key,
                ValidateLifetime = true, ClockSkew = TimeSpan.Zero
            };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(AdminVendorController).Assembly);
        host._app = builder.Build();
        host._app.UseAuthentication();
        host._app.UseAuthorization();
        host._app.MapControllers();
        await host._app.StartAsync();
        host._client = host._app.GetTestClient();
        return host;
    }

    public Task<HttpResponseMessage> Send(string path, string? role)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (role is not null)
        {
            var token = new JwtSecurityToken("vendor-admin-tests", "vendor-admin-tests", [new(JwtRegisteredClaimNames.Sub, "1"), new(ClaimTypes.Role, role)],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(_key, SecurityAlgorithms.HmacSha256));
            request.Headers.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        return _client.SendAsync(request);
    }

    public async ValueTask DisposeAsync() { _client.Dispose(); await _app.DisposeAsync(); }
}
