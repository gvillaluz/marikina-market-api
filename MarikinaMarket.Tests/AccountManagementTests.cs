using System.Collections;
using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
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
using Npgsql;
using Xunit;

namespace MarikinaMarket.Tests;

public class AccountManagementTests
{
    [Fact]
    public async Task Counts_include_every_user_but_directory_excludes_actor_before_paging_and_total()
    {
        await using var host = await AccountManagementHost.Create();
        using var counts = await host.Send(HttpMethod.Get, "/api/admin/accounts/counts");
        Assert.Equal(HttpStatusCode.OK, counts.StatusCode);
        var metrics = await counts.Content.ReadFromJsonAsync<JsonElement>();
        // 13 users: 12 staff plus one roleless inactive account. One staff user
        // also has vendor membership; joining roles must not inflate Total Users.
        Assert.Equal(13, metrics.GetProperty("total_users").GetInt32());
        Assert.Equal(12, metrics.GetProperty("total_staff_users").GetInt32());
        using var first = await host.Send(HttpMethod.Get, "/api/admin/accounts?offset=0");
        var page = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(12, page.GetProperty("total").GetInt32());
        Assert.Equal(10, page.GetProperty("items").GetArrayLength());
        Assert.True(page.GetProperty("has_more").GetBoolean());
        Assert.DoesNotContain(page.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt32() == 1);
        using var last = await host.Send(HttpMethod.Get, "/api/admin/accounts?offset=10");
        var end = await last.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, end.GetProperty("items").GetArrayLength());
        Assert.False(end.GetProperty("has_more").GetBoolean());
        using var filtered = await host.Send(HttpMethod.Get, "/api/admin/accounts?role=HeadAdmin&search=Reyes");
        var match = await filtered.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, match.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Admin_edit_loads_full_form_saves_target_and_rejects_stale_or_duplicate_email()
    {
        await using var host = await AccountManagementHost.Create();
        using var get = await host.Send(HttpMethod.Get, "/api/admin/users/2");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var before = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Sampaguita", before.GetProperty("street").GetString());
        Assert.Equal(5, before.GetProperty("version").GetInt32());
        Assert.False(before.TryGetProperty("password_hash", out _));
        var edit = host.Edit();
        edit["role"] = "MarketVendor"; edit["username"] = "tampered"; edit["status"] = "Inactive";
        using var saved = await host.Send(HttpMethod.Put, "/api/admin/users/2", body: edit);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("Updated", host.Users[1].FirstName);
        Assert.Equal("HAD0002-test", host.Users[1].UserName);
        Assert.Equal(AccountStatus.Active, host.Users[1].Status);
        Assert.Equal("HeadAdmin", host.Memberships[2][0]);
        Assert.Equal("User1", host.Users[0].FirstName);
        Assert.Equal(1, host.Writes);
        Assert.Equal(1, host.Commits);
        Assert.False(host.Users[1].EmailConfirmed);
        using var stale = await host.Send(HttpMethod.Put, "/api/admin/users/2", body: host.Edit());
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var duplicate = host.Edit(); duplicate["version"] = 6; duplicate["email"] = host.Users[0].Email;
        using var conflict = await host.Send(HttpMethod.Put, "/api/admin/users/2", body: duplicate);
        Assert.Equal(HttpStatusCode.BadRequest, conflict.StatusCode);
        Assert.Equal(1, host.Writes);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("AdminOfficer", 403)]
    [InlineData("MarketEnforcer", 403)]
    [InlineData("MarketVendor", 403)]
    public async Task Account_routes_require_head_admin(string? role, int status)
    {
        await using var host = await AccountManagementHost.Create();
        foreach (var (method, path) in new[] {
            (HttpMethod.Get, "/api/admin/accounts"), (HttpMethod.Get, "/api/admin/accounts/counts"),
            (HttpMethod.Get, "/api/admin/users/2"), (HttpMethod.Put, "/api/admin/users/2") })
        {
            using var response = await host.Send(method, path, role, method == HttpMethod.Put ? host.Edit() : null);
            Assert.Equal(status, (int)response.StatusCode);
        }
        Assert.Equal(0, host.Writes);
    }

    [Fact]
    public async Task Self_edit_missing_user_and_invalid_fields_do_not_write()
    {
        await using var host = await AccountManagementHost.Create();
        using var self = await host.Send(HttpMethod.Put, "/api/admin/users/1", body: host.Edit());
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        using var absent = await host.Send(HttpMethod.Get, "/api/admin/users/999");
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        var invalid = host.Edit(); invalid["first_name"] = " ";
        using var bad = await host.Send(HttpMethod.Put, "/api/admin/users/2", body: invalid);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal(0, host.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Competing_save_returns_conflict_without_commit(bool serializationFailure)
    {
        await using var host = await AccountManagementHost.Create();
        host.CompetingSave = serializationFailure;
        using var response = await host.Send(HttpMethod.Put, "/api/admin/users/2", body: host.Edit());
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, host.Commits);
        Assert.Equal(0, host.Writes);
    }
}

internal sealed class AccountManagementHost : IAsyncDisposable
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;
    private AppDbContext _context = null!;
    private readonly SymmetricSecurityKey _key = new(RandomNumberGenerator.GetBytes(32));
    public List<User> Users { get; } = Enumerable.Range(1, 13).Select(id => new User {
        Id = id, UserName = $"HAD{id:D4}-test", FirstName = $"User{id}", LastName = "Reyes",
        Email = $"user{id}@example.invalid", EmailConfirmed = true, DateOfBirth = new(1990, 1, 1),
        PhoneNumber = "09171234567", HouseNumber = "12", Street = "Sampaguita", Barangay = "Santa Elena",
        City = "Marikina", Status = id == 13 ? AccountStatus.Inactive : AccountStatus.Active, Version = 5
    }).ToList();
    public Dictionary<int, List<string>> Memberships { get; } = Enumerable.Range(1, 12).ToDictionary(id => id,
        id => new List<string> { id <= 2 ? "HeadAdmin" : id <= 8 ? "MarketEnforcer" : "AdminOfficer" });
    public int Writes;
    public int Commits;
    public bool? CompetingSave;

    public static async Task<AccountManagementHost> Create()
    {
        var host = new AccountManagementHost();
        host.Memberships[2].Add("MarketVendor");
        host._context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options) {
            Users = new AccountDatabaseSet<User>(host.Users),
            Roles = new AccountDatabaseSet<IdentityRole<int>>([new("HeadAdmin") { Id = 1 }, new("MarketVendor") { Id = 2 }, new("MarketEnforcer") { Id = 3 }, new("AdminOfficer") { Id = 4 }]),
            UserRoles = new AccountDatabaseSet<IdentityUserRole<int>>(host.Memberships.SelectMany(pair => pair.Value.Select(role => new IdentityUserRole<int> { UserId = pair.Key, RoleId = role switch { "HeadAdmin" => 1, "MarketVendor" => 2, "MarketEnforcer" => 3, _ => 4 } })).ToList())
        };
        var store = ReviewBoundaryProxy.Create<IVendorProfileIdentityStore>((m, a) => host.Identity(m, a));
        var manager = new UserManager<User>(store, Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(), [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<UserManager<User>>.Instance);
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IUserRepository>(new UserRepository(manager, null!, host._context));
        builder.Services.AddSingleton(ReviewBoundaryProxy.Create<IStorageService>((m, a) => Task.FromResult(new Dictionary<string, string>())));
        builder.Services.AddSingleton(ReviewBoundaryProxy.Create<IEmailService>((m, a) => throw new InvalidOperationException("Editing must not send credentials")));
        builder.Services.AddSingleton(ReviewBoundaryProxy.Create<IUnitOfWork>((m, a) => {
            if (m.Name == "CommitAsync") host.Commits++;
            return m.Name == "Dispose" ? null : Task.CompletedTask;
        }));
        builder.Services.AddSingleton<IUserService, UserService>(); builder.Services.AddSingleton<IAdminUserService, AdminUserService>();
        builder.Services.AddTransient<ExceptionHandlingMiddleware>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new() {
            ValidateIssuer = true, ValidIssuer = "account-tests", ValidateAudience = true, ValidAudience = "account-tests",
            ValidateIssuerSigningKey = true, IssuerSigningKey = host._key, ValidateLifetime = true, ClockSkew = TimeSpan.Zero
        });
        builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(AdminAccountController).Assembly).AddJsonOptions(o => {
            o.JsonSerializerOptions.PropertyNamingPolicy = new SnakeCaseNamingPolicy();
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
        host._app = builder.Build(); host._app.UseMiddleware<ExceptionHandlingMiddleware>();
        host._app.UseAuthentication(); host._app.UseAuthorization(); host._app.MapControllers();
        await host._app.StartAsync(); host._client = host._app.GetTestClient(); return host;
    }

    public Dictionary<string, object?> Edit() => new() {
        ["version"] = 5, ["first_name"] = "Updated", ["middle_name"] = null, ["last_name"] = "Reyes",
        ["date_of_birth"] = "1990-01-01", ["email"] = "updated@example.invalid", ["phone_number"] = "09171234567",
        ["house_number"] = "12", ["street"] = "Sampaguita", ["barangay"] = "Santa Elena", ["city"] = "Marikina"
    };
    public Task<HttpResponseMessage> Send(HttpMethod method, string path, string? role = "HeadAdmin", Dictionary<string, object?>? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (role is not null) {
            var token = new JwtSecurityToken("account-tests", "account-tests", [new(JwtRegisteredClaimNames.Sub, "1"), new(ClaimTypes.Role, role)],
                expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new(_key, SecurityAlgorithms.HmacSha256));
            request.Headers.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        return _client.SendAsync(request);
    }
    private object? Identity(MethodInfo method, object?[] args)
    {
        var user = args.FirstOrDefault() as User;
        switch (method.Name) {
            case "FindByIdAsync": return Task.FromResult(Users.SingleOrDefault(u => u.Id.ToString() == (string)args[0]!));
            case "FindByEmailAsync": return Task.FromResult(Users.SingleOrDefault(u => u.Email!.ToUpperInvariant() == (string)args[0]!));
            case "GetRolesAsync": return Task.FromResult<IList<string>>(Memberships.GetValueOrDefault(user!.Id) ?? []);
            case "GetUserNameAsync": return Task.FromResult(user!.UserName);
            case "GetEmailAsync": return Task.FromResult(user!.Email);
            case "SetNormalizedUserNameAsync": case "SetNormalizedEmailAsync": return Task.CompletedTask;
            case "UpdateAsync":
                if (CompetingSave == true)
                    throw new DbUpdateException("Synthetic conflicting save", new PostgresException("Synthetic conflict", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure));
                if (CompetingSave == false)
                    return Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "ConcurrencyFailure" }));
                Writes++; user!.Version++; return Task.FromResult(IdentityResult.Success);
            case "Dispose": return null;
            default: throw new InvalidOperationException(method.Name);
        }
    }
    public async ValueTask DisposeAsync() { _client.Dispose(); await _app.DisposeAsync(); await _context.DisposeAsync(); }
}

// Synthetic persistence boundary; production repository expressions execute unchanged.
internal sealed class AccountDatabaseSet<T>(IEnumerable<T> records) : DbSet<T>, IQueryable<T>, IAsyncEnumerable<T> where T : class
{
    private readonly IQueryable<T> _source = records.AsQueryable();
    public override Microsoft.EntityFrameworkCore.Metadata.IEntityType EntityType => throw new NotSupportedException();
    Type IQueryable.ElementType => typeof(T);
    Expression IQueryable.Expression => _source.Expression;
    IQueryProvider IQueryable.Provider => new AccountQueryProvider(_source.Provider);
    public override IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new AccountAsyncEnumerator<T>(_source.GetEnumerator());
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => _source.GetEnumerator();
}
internal sealed class AccountQueryProvider(IQueryProvider inner) : IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => inner.CreateQuery(expression);
    public IQueryable<T> CreateQuery<T>(Expression expression) => new AccountQuery<T>(expression);
    public object? Execute(Expression expression) => inner.Execute(expression);
    public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);
    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default) =>
        (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(typeof(TResult).GetGenericArguments()[0]).Invoke(null, [inner.Execute(expression)])!;
}
internal sealed class AccountQuery<T>(Expression expression) : EnumerableQuery<T>(expression), IAsyncEnumerable<T>, IQueryable<T>
{
    IQueryProvider IQueryable.Provider => new AccountQueryProvider(this);
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new AccountAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
}
internal sealed class AccountAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;
    public ValueTask DisposeAsync() { inner.Dispose(); return ValueTask.CompletedTask; }
    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
}
