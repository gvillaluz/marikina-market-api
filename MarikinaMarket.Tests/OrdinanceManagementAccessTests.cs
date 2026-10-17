using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
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

public class OrdinanceManagementAccessTests
{
    [Theory]
    [InlineData("HeadAdmin", 200, 200)]
    [InlineData("AdminOfficer", 200, 200)]
    [InlineData("MarketEnforcer", 200, 403)]
    [InlineData("MarketVendor", 403, 403)]
    [InlineData(null, 401, 401)]
    public async Task Ordinance_roles_allow_staff_lookup_but_only_administrators_manage(string? role, int readStatus, int manageStatus)
    {
        await using var host = await OrdinanceManagementAccessHost.Create();
        using var list = await host.Send(HttpMethod.Get, "/api/ordinance", role);
        using var count = await host.Send(HttpMethod.Get, "/api/ordinance/count", role);
        using var details = await host.Send(HttpMethod.Get, "/api/ordinance/7", role);
        using var create = await host.Send(HttpMethod.Post, "/api/ordinance", role, OrdinanceManagementAccessHost.Request());
        using var update = await host.Send(HttpMethod.Put, "/api/ordinance/7", role, OrdinanceManagementAccessHost.Request());
        Assert.Equal(readStatus, (int)list.StatusCode);
        Assert.Equal(readStatus, (int)count.StatusCode);
        Assert.Equal(manageStatus, (int)details.StatusCode);
        Assert.Equal(manageStatus == 200 ? 201 : manageStatus, (int)create.StatusCode);
        Assert.Equal(manageStatus, (int)update.StatusCode);
        Assert.Equal(manageStatus == 200 ? 2 : 0, host.Commits);
    }

    [Fact]
    public async Task Empty_list_contract_allows_administrator_to_create_first_ordinance_and_read_its_tiers()
    {
        await using var host = await OrdinanceManagementAccessHost.Create();
        host.Items.Clear();
        using var empty = await host.Send(HttpMethod.Get, "/api/ordinance", "AdminOfficer");
        Assert.Equal(HttpStatusCode.NotFound, empty.StatusCode);
        Assert.Contains("Ordinance list not found.", await empty.Content.ReadAsStringAsync());
        using var create = await host.Send(HttpMethod.Post, "/api/ordinance", "AdminOfficer", OrdinanceManagementAccessHost.Request());
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt32();
        using var details = await host.Send(HttpMethod.Get, $"/api/ordinance/{id}", "AdminOfficer");
        var loaded = await details.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("011", loaded.GetProperty("ordinance_no").GetString());
        Assert.Equal(500, loaded.GetProperty("penalty_tiers")[0].GetProperty("penalty_amount").GetDecimal());
        using var list = await host.Send(HttpMethod.Get, "/api/ordinance", "MarketEnforcer");
        Assert.Single((await list.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        using var count = await host.Send(HttpMethod.Get, "/api/ordinance/count", "MarketEnforcer");
        Assert.Equal(1, (await count.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("count").GetInt32());
    }
}

// Real authentication/controller/service; only repository and transaction storage
// are controlled. This does not establish PostgreSQL durability or live E2E.
internal sealed class OrdinanceManagementAccessHost : IAsyncDisposable
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly SymmetricSecurityKey _key = new(RandomNumberGenerator.GetBytes(32));
    public List<Ordinance> Items { get; } = [new() { Id = 7, OrdinanceNo = "011", Series = "2026", MarketCode = "MKT-011", Title = "Market sanitation", Category = ViolationCategory.Sanitation }];
    public int Commits;

    public static async Task<OrdinanceManagementAccessHost> Create()
    {
        var host = new OrdinanceManagementAccessHost();
        var repository = ReviewBoundaryProxy.Create<IOrdinanceRepository>((method, args) =>
        {
            switch (method.Name)
            {
                case "GetOrdinances": return Task.FromResult(host.Items.ToList());
                case "GetActiveCountAsync": return Task.FromResult(host.Items.Count(item => item.IsActive));
                case "GetByIdAsync": return Task.FromResult(host.Items.SingleOrDefault(item => item.Id == (int)args[0]!));
                case "AddAsync":
                    var ordinance = (Ordinance)args[0]!;
                    ordinance.Id = host.Items.Select(item => item.Id).DefaultIfEmpty(0).Max() + 1;
                    host.Items.Add(ordinance);
                    return Task.CompletedTask;
                case "AddTiersAsync": return Task.CompletedTask;
                case "RemoveTiers": return null;
                default: throw new InvalidOperationException(method.Name);
            }
        });
        var transactions = ReviewBoundaryProxy.Create<IUnitOfWork>((method, _) =>
        {
            if (method.Name == "CommitAsync") host.Commits++;
            return method.Name switch
            {
                "SaveChangesAsync" => Task.FromResult(1),
                "Dispose" => null,
                _ => Task.CompletedTask
            };
        });
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IOrdinanceService>(new OrdinanceService(repository, transactions));
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = "ordinance-tests", ValidateAudience = true, ValidAudience = "ordinance-tests",
                ValidateIssuerSigningKey = true, IssuerSigningKey = host._key, ValidateLifetime = true, ClockSkew = TimeSpan.Zero
            };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(OrdinanceController).Assembly).AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        host._app = builder.Build();
        host._app.UseAuthentication();
        host._app.UseAuthorization();
        host._app.MapControllers();
        await host._app.StartAsync();
        host._client = host._app.GetTestClient();
        return host;
    }

    public static object Request() => new
    {
        ordinance_number = "011", series = "2026", market_code = "MKT-011", title = "Market sanitation", category = "Sanitation",
        penalty_tiers = new[] { new { offense_number = 1, severity = "Minor", penalty_amount = 500 } }
    };

    public Task<HttpResponseMessage> Send(HttpMethod method, string path, string? role, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (role is not null)
        {
            var token = new JwtSecurityToken("ordinance-tests", "ordinance-tests", [new(JwtRegisteredClaimNames.Sub, "1"), new(ClaimTypes.Role, role)],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(_key, SecurityAlgorithms.HmacSha256));
            request.Headers.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        return _client.SendAsync(request);
    }

    public async ValueTask DisposeAsync() { _client.Dispose(); await _app.DisposeAsync(); }
}
