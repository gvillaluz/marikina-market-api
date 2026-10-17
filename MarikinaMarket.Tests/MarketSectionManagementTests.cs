using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Infrastructure.Json;
using MarikinaMarket.API.Presentation.Controllers;
using MarikinaMarket.API.Presentation.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace MarikinaMarket.Tests;

public class MarketSectionManagementTests
{
    [Theory]
    [InlineData("HeadAdmin", 200, 200)]
    [InlineData("AdminOfficer", 200, 200)]
    [InlineData("MarketEnforcer", 200, 403)]
    [InlineData("MarketVendor", 200, 403)]
    [InlineData(null, 401, 401)]
    public async Task Only_administrators_manage_while_existing_authenticated_lookup_access_is_preserved(string? role, int readStatus, int manageStatus)
    {
        await using var host = await MarketSectionManagementHost.Create();
        using var list = await host.Send(HttpMethod.Get, "/api/marketsection", role);
        using var count = await host.Send(HttpMethod.Get, "/api/marketsection/count", role);
        using var create = await host.Send(HttpMethod.Post, "/api/marketsection", role, new { name = "Bakery", description = "Bread stalls" });
        using var edit = await host.Send(HttpMethod.Put, "/api/marketsection/2", role, new { name = "Produce", description = "Produce stalls" });
        using var status = await host.Send(HttpMethod.Patch, "/api/marketsection/2/active", role, new { is_active = false });
        Assert.Equal(readStatus, (int)list.StatusCode);
        Assert.Equal(readStatus, (int)count.StatusCode);
        Assert.Equal(manageStatus == 200 ? 201 : manageStatus, (int)create.StatusCode);
        Assert.Equal(manageStatus, (int)edit.StatusCode);
        Assert.Equal(manageStatus == 200 ? 204 : manageStatus, (int)status.StatusCode);
        Assert.Equal(manageStatus == 200 ? 3 : 0, host.Writes);
    }

    [Fact]
    public async Task Invalid_lengths_and_duplicate_names_are_rejected_without_writes()
    {
        await using var host = await MarketSectionManagementHost.Create();
        foreach (var draft in new[] {
            new { name = " ", description = "Produce stalls" }, new { name = new string('a', 151), description = "Produce stalls" },
            new { name = "Bakery", description = new string('a', 501) }, new { name = "Fresh Produce", description = "Duplicate name" }
        })
        {
            using var response = await host.Send(HttpMethod.Post, "/api/marketsection", "AdminOfficer", draft);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var invalidId = await host.Send(HttpMethod.Patch, "/api/marketsection/0/active", "HeadAdmin", new { is_active = true });
        Assert.Equal(HttpStatusCode.BadRequest, invalidId.StatusCode);
        using var absent = await host.Send(HttpMethod.Put, "/api/marketsection/999", "HeadAdmin", new { name = "Bakery", description = "Bread stalls" });
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        Assert.Equal(0, host.Writes);
    }

    [Fact]
    public async Task Empty_list_create_edit_and_both_status_directions_keep_list_and_count_consistent()
    {
        await using var host = await MarketSectionManagementHost.Create();
        host.Items.Clear();
        using var empty = await host.Send(HttpMethod.Get, "/api/marketsection", "AdminOfficer");
        Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        Assert.Empty((await empty.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        using var create = await host.Send(HttpMethod.Post, "/api/marketsection", "AdminOfficer", new { name = " Bakery ", description = " Bread stalls " });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt32();
        Assert.Equal("Bakery", created.GetProperty("name").GetString());
        using var edit = await host.Send(HttpMethod.Put, $"/api/marketsection/{id}", "AdminOfficer", new { name = "Baked goods", description = "Bread and pastry stalls" });
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        foreach (var active in new[] { false, true })
        {
            using var status = await host.Send(HttpMethod.Patch, $"/api/marketsection/{id}/active", "AdminOfficer", new { is_active = active });
            Assert.Equal(HttpStatusCode.NoContent, status.StatusCode);
            using var list = await host.Send(HttpMethod.Get, "/api/marketsection", "MarketEnforcer");
            var records = await list.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Single(records.EnumerateArray());
            Assert.Equal("Baked goods", records[0].GetProperty("name").GetString());
            Assert.Equal(active, records[0].GetProperty("is_active").GetBoolean());
            using var count = await host.Send(HttpMethod.Get, "/api/marketsection/count", "MarketEnforcer");
            Assert.Equal(active ? 1 : 0, (await count.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("count").GetInt32());
        }
    }
}

// Real JWT/controller/service and production JSON naming/middleware, with only
// repository storage controlled. No database, migrations or live startup.
internal sealed class MarketSectionManagementHost : IAsyncDisposable
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private readonly SymmetricSecurityKey _key = new(RandomNumberGenerator.GetBytes(32));
    public List<MarketSection> Items { get; } = [new() { Id = 2, Name = "Fresh Produce", Description = "Fruit and vegetable stalls" }];
    public int Writes;

    public static async Task<MarketSectionManagementHost> Create()
    {
        var host = new MarketSectionManagementHost();
        var repository = ReviewBoundaryProxy.Create<IMarketSectionRepository>((method, args) =>
        {
            switch (method.Name)
            {
                case "GetAllAsync": return Task.FromResult(host.Items.OrderBy(item => item.Name).ToList());
                case "GetById": return Task.FromResult(host.Items.SingleOrDefault(item => item.Id == (int)args[0]!));
                case "GetActiveCountAsync": return Task.FromResult(host.Items.Count(item => item.IsActive));
                case "NameExistsAsync": return Task.FromResult(host.Items.Any(item => item.Name == (string)args[0]! && item.Id != (int?)args[1]));
                case "GetVendorCountAsync": return Task.FromResult(0);
                case "AddAsync":
                    var section = (MarketSection)args[0]!;
                    section.Id = host.Items.Select(item => item.Id).DefaultIfEmpty(0).Max() + 1;
                    host.Items.Add(section);
                    return Task.CompletedTask;
                case "SaveChangesAsync": host.Writes++; return Task.CompletedTask;
                default: throw new InvalidOperationException(method.Name);
            }
        });
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IMarketSectionService>(new MarketSectionService(repository));
        builder.Services.AddTransient<ExceptionHandlingMiddleware>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = "section-tests", ValidateAudience = true, ValidAudience = "section-tests",
                ValidateIssuerSigningKey = true, IssuerSigningKey = host._key, ValidateLifetime = true, ClockSkew = TimeSpan.Zero
            };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(MarketSectionController).Assembly).AddJsonOptions(options =>
            options.JsonSerializerOptions.PropertyNamingPolicy = new SnakeCaseNamingPolicy());
        host._app = builder.Build();
        host._app.UseMiddleware<ExceptionHandlingMiddleware>();
        host._app.UseAuthentication();
        host._app.UseAuthorization();
        host._app.MapControllers();
        await host._app.StartAsync();
        host._client = host._app.GetTestClient();
        return host;
    }

    public Task<HttpResponseMessage> Send(HttpMethod method, string path, string? role, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (role is not null)
        {
            var token = new JwtSecurityToken("section-tests", "section-tests", [new(JwtRegisteredClaimNames.Sub, "1"), new(ClaimTypes.Role, role)],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(_key, SecurityAlgorithms.HmacSha256));
            request.Headers.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        return _client.SendAsync(request);
    }

    public async ValueTask DisposeAsync() { _client.Dispose(); await _app.DisposeAsync(); }
}
