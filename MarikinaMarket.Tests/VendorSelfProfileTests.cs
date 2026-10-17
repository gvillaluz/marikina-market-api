using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Json;
using MarikinaMarket.API.Infrastructure.Persistence;
using MarikinaMarket.API.Infrastructure.Repositories;
using MarikinaMarket.API.Presentation.Controllers;
using MarikinaMarket.API.Presentation.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace MarikinaMarket.Tests;

public class VendorSelfProfileTests
{
    [Fact]
    public async Task Read_and_edit_use_account_ownership_and_include_both_profiles()
    {
        await using var host = await VendorSelfProfileHost.CreateAsync();
        using var before = await host.Send(HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var profile = await before.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(7, profile.GetProperty("user").GetProperty("userId").GetInt32());
        var vendor = profile.GetProperty("vendor");
        Assert.Equal(70, vendor.GetProperty("vendor_id").GetInt32());
        Assert.Equal("Reyes Fresh Fish", vendor.GetProperty("business_name").GetString());
        Assert.Equal("Fish and Seafood", vendor.GetProperty("market_section_name").GetString());
        Assert.Equal("F-12", vendor.GetProperty("stall_number").GetString());
        Assert.Equal(92, vendor.GetProperty("compliance_score").GetInt32());
        Assert.False(profile.GetProperty("user").TryGetProperty("passwordHash", out _));
        using var updated = await host.Send(HttpMethod.Put, body: host.Edit());
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var result = await updated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Juana", result.GetProperty("user").GetProperty("firstName").GetString());
        Assert.Equal("MM-70", result.GetProperty("vendor").GetProperty("business_id").GetString());
        using var refreshed = await host.Send(HttpMethod.Get);
        var fresh = await refreshed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Juana", fresh.GetProperty("user").GetProperty("firstName").GetString());
        Assert.Equal("Santos", host.Users.Single(u => u.Id == 88).LastName);
        Assert.Equal("other@example.invalid", host.Users.Single(u => u.Id == 88).Email);
        Assert.False(host.Users.Single(u => u.Id == 7).EmailConfirmed);
    }

    [Theory]
    [InlineData(null, "7", 401)]
    [InlineData("MarketEnforcer", "7", 403)]
    [InlineData("HeadAdmin", "7", 403)]
    [InlineData("MarketVendor", "invalid", 401)]
    [InlineData("MarketVendor", "0", 401)]
    public async Task Both_endpoints_enforce_vendor_identity(string? role, string id, int expected)
    {
        await using var host = await VendorSelfProfileHost.CreateAsync();
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Put })
        {
            using var response = await host.Send(method, role, id, method == HttpMethod.Put ? host.Edit() : null);
            Assert.Equal(expected, (int)response.StatusCode);
        }
        Assert.Equal(0, host.Writes);
    }

    [Theory]
    [InlineData("phone_number", "123")]
    [InlineData("email", "not-an-email")]
    [InlineData("email", "other@example.invalid")]
    [InlineData("date_of_birth", "2999-01-01")]
    [InlineData("first_name", " ")]
    public async Task Invalid_edits_do_not_write(string field, string value)
    {
        await using var host = await VendorSelfProfileHost.CreateAsync();
        var edit = host.Edit(); edit[field] = value;
        using var response = await host.Send(HttpMethod.Put, body: edit);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, host.Writes);
        Assert.Equal("Juan", host.Users.Single(u => u.Id == 7).FirstName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_vendor_or_account_returns_not_found_without_writes(bool removeAccount)
    {
        await using var host = await VendorSelfProfileHost.CreateAsync();
        if (removeAccount) host.Users.RemoveAll(u => u.Id == 7);
        else host.Vendors.RemoveAll(v => v.UserId == 7);
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Put })
        {
            using var response = await host.Send(method, body: method == HttpMethod.Put ? host.Edit() : null);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        Assert.Equal(0, host.Writes);
    }

    [Fact]
    public async Task Supplied_identity_and_protected_fields_cannot_change_ownership_or_business()
    {
        await using var host = await VendorSelfProfileHost.CreateAsync();
        var edit = host.Edit();
        edit["user_id"] = "88"; edit["vendor_id"] = "7";
        edit["role"] = "HeadAdmin"; edit["status"] = "Inactive";
        edit["username"] = "changed"; edit["business_name"] = "changed";
        edit["stall_number"] = "changed"; edit["compliance_score"] = "0";
        using var response = await host.Send(HttpMethod.Put, body: edit);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MarketVendor", result.GetProperty("user").GetProperty("role").GetString());
        Assert.Equal("vendor-7", result.GetProperty("user").GetProperty("username").GetString());
        Assert.Equal("Reyes Fresh Fish", result.GetProperty("vendor").GetProperty("business_name").GetString());
        Assert.Equal("Active", result.GetProperty("vendor").GetProperty("status").GetString());
        Assert.Equal("F-12", result.GetProperty("vendor").GetProperty("stall_number").GetString());
        Assert.Equal(92, result.GetProperty("vendor").GetProperty("compliance_score").GetInt32());
        Assert.Equal("Maria", host.Users.Single(u => u.Id == 88).FirstName);
    }
}

internal sealed class VendorSelfProfileHost : IAsyncDisposable
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private AppDbContext _context = null!;
    private readonly SymmetricSecurityKey _key = new(RandomNumberGenerator.GetBytes(32));
    public List<User> Users { get; } = [Account(7, "Juan", "Reyes", "juan@example.invalid"), Account(88, "Maria", "Santos", "other@example.invalid")];
    public List<VendorProfile> Vendors { get; } = [];
    public int Writes;

    public static async Task<VendorSelfProfileHost> CreateAsync()
    {
        var host = new VendorSelfProfileHost();
        host.Vendors.Add(new VendorProfile
        {
            Id = 70, UserId = 7, BusinessId = "MM-70", BusinessName = "Reyes Fresh Fish", StallNumber = "F-12",
            MarketSectionId = 2, MarketSection = new() { Id = 2, Name = "Fish and Seafood", Description = "Fish retail" },
            Status = VendorStatus.Active, Type = VendorType.Public, ComplianceScore = 92, QrCodeValue = "test-only",
            RegisteredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        host.Vendors.Add(new VendorProfile { Id = 7, UserId = 88, BusinessId = "MM-7", BusinessName = "Santos Meat", QrCodeValue = "other" });
        host._context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options)
        { VendorProfiles = new VendorSelfProfileDatabaseBoundary(host.Vendors) };
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders(); builder.Logging.AddConsole();
        // Replace only persistence/Identity storage and external storage boundaries.
        var store = ReviewBoundaryProxy.Create<IVendorProfileIdentityStore>((m, a) => host.Identity(m, a));
        var manager = new UserManager<User>(store, Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(), [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
            null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<UserManager<User>>.Instance);
        builder.Services.AddSingleton<IUserRepository>(new UserRepository(manager, null!, host._context));
        builder.Services.AddSingleton<IVendorRepository>(new VendorRepository(host._context));
        builder.Services.AddSingleton(ReviewBoundaryProxy.Create<IStorageService>((m, a) => throw new InvalidOperationException(m.Name)));
        builder.Services.AddSingleton(ReviewBoundaryProxy.Create<IUnitOfWork>((m, a) => m.Name == "Dispose" ? null : Task.CompletedTask));
        builder.Services.AddSingleton(ReviewBoundaryProxy.Create<IMarketSectionRepository>((m, a) => throw new InvalidOperationException(m.Name)));
        builder.Services.AddSingleton(ReviewBoundaryProxy.Create<IVendorComplianceScoreService>((m, a) => throw new InvalidOperationException(m.Name)));
        builder.Services.AddSingleton(ReviewBoundaryProxy.Create<IEmailService>((m, a) => throw new InvalidOperationException(m.Name)));
        builder.Services.AddSingleton(ReviewBoundaryProxy.Create<INotificationService>((m, a) => throw new InvalidOperationException(m.Name)));
        builder.Services.AddSingleton<IUserService, UserService>();
        builder.Services.AddSingleton<IVendorService, VendorService>();
        builder.Services.AddTransient<ExceptionHandlingMiddleware>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = "profile-tests", ValidateAudience = true, ValidAudience = "profile-tests",
                ValidateIssuerSigningKey = true, IssuerSigningKey = host._key, ValidateLifetime = true, ClockSkew = TimeSpan.Zero
            };
        });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(VendorController).Assembly).AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = new SnakeCaseNamingPolicy();
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        host._app = builder.Build();
        host._app.UseMiddleware<ExceptionHandlingMiddleware>(); host._app.UseAuthentication(); host._app.UseAuthorization(); host._app.MapControllers();
        await host._app.StartAsync(); host._client = host._app.GetTestClient();
        return host;
    }

    public Dictionary<string, string> Edit() => new()
    {
        ["first_name"] = " Juana ", ["last_name"] = "Reyes", ["middle_name"] = "",
        ["date_of_birth"] = "1990-01-01", ["email"] = "juana@example.invalid", ["phone_number"] = "09171234567",
        ["house_number"] = "12", ["street"] = "Sampaguita", ["barangay"] = "Santa Elena", ["city"] = "Marikina"
    };

    public Task<HttpResponseMessage> Send(HttpMethod method, string? role = "MarketVendor", string id = "7", Dictionary<string, string>? body = null, string? path = null)
    {
        var request = new HttpRequestMessage(method, path ?? "/api/vendor/profile?user_id=88&vendor_id=7");
        if (body is not null) request.Content = JsonContent.Create(body);
        if (role is not null)
        {
            var token = new JwtSecurityToken("profile-tests", "profile-tests", [new(JwtRegisteredClaimNames.Sub, id), new(ClaimTypes.Role, role)],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(_key, SecurityAlgorithms.HmacSha256));
            request.Headers.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        return _client.SendAsync(request);
    }

    private object? Identity(MethodInfo method, object?[] args)
    {
        var user = args.FirstOrDefault() as User;
        return method.Name switch
        {
            "FindByIdAsync" => Task.FromResult(Users.SingleOrDefault(u => u.Id.ToString() == (string)args[0]!)),
            "FindByEmailAsync" => Task.FromResult(Users.SingleOrDefault(u => u.Email!.ToUpperInvariant() == (string)args[0]!)),
            "GetRolesAsync" => Task.FromResult<IList<string>>(["MarketVendor"]),
            "GetUserNameAsync" => Task.FromResult(user!.UserName),
            "GetEmailAsync" => Task.FromResult(user!.Email),
            "SetNormalizedUserNameAsync" or "SetNormalizedEmailAsync" => Task.CompletedTask,
            "UpdateAsync" => Write(),
            "Dispose" => null,
            _ => throw new InvalidOperationException(method.Name)
        };
    }
    private Task<IdentityResult> Write() { Writes++; return Task.FromResult(IdentityResult.Success); }
    private static User Account(int id, string first, string last, string email) => new()
    {
        Id = id, UserName = $"vendor-{id}", FirstName = first, LastName = last, Email = email, EmailConfirmed = true,
        DateOfBirth = new(1990, 1, 1), PhoneNumber = "09171234567", HouseNumber = "10", Street = "Sample",
        Barangay = "Santa Elena", City = "Marikina", Status = AccountStatus.Active
    };
    public async ValueTask DisposeAsync()
    {
        _client.Dispose(); await _app.DisposeAsync(); await _context.DisposeAsync();
    }
}

public interface IVendorProfileIdentityStore : IUserEmailStore<User>, IUserRoleStore<User> { }

// Execute the real repository predicate against the synthetic database boundary.
internal sealed class VendorSelfProfileDatabaseBoundary(List<VendorProfile> records)
    : DbSet<VendorProfile>, IQueryable<VendorProfile>, IAsyncQueryProvider
{
    private readonly IQueryable<VendorProfile> _source = records.AsQueryable();
    public override Microsoft.EntityFrameworkCore.Metadata.IEntityType EntityType => throw new NotSupportedException();
    Type IQueryable.ElementType => typeof(VendorProfile);
    Expression IQueryable.Expression => _source.Expression;
    IQueryProvider IQueryable.Provider => this;
    IEnumerator<VendorProfile> IEnumerable<VendorProfile>.GetEnumerator() => _source.GetEnumerator();
    public IQueryable CreateQuery(Expression expression) => _source.Provider.CreateQuery(expression);
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) => _source.Provider.CreateQuery<TElement>(expression);
    public object? Execute(Expression expression) => _source.Provider.Execute(expression);
    public TResult Execute<TResult>(Expression expression) => _source.Provider.Execute<TResult>(expression);
    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return (TResult)(object)Task.FromResult(_source.Provider.Execute<VendorProfile?>(expression));
    }
}
