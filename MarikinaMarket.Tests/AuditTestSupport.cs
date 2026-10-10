using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MarikinaMarket.API.Application;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;
using MarikinaMarket.API.Application.DTOs.Audits.Request;
using MarikinaMarket.API.Application.DTOs.Audits.Response;
using MarikinaMarket.API.Application.DTOs.Auth.Response;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Json;
using MarikinaMarket.API.Presentation.Controllers;
using MarikinaMarket.API.Presentation.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Module = MarikinaMarket.API.Domain.Enums.Module;

namespace MarikinaMarket.Tests;

public class AuditUserRepositoryStub : DispatchProxy
{
    public Dictionary<int, UserNamesResponse> Users { get; } = [];
    public List<int[]> NameRequests { get; } = [];

    public static IUserRepository Create()
    {
        var repository = DispatchProxy.Create<IUserRepository, AuditUserRepositoryStub>();
        var stub = (AuditUserRepositoryStub)(object)repository;
        stub.Users[7] = new UserNamesResponse { Id = 7, FirstName = "Juan", LastName = "Dela Cruz" };
        stub.Users[8] = new UserNamesResponse { Id = 8, FirstName = "Maria", LastName = "Santos" };
        return repository;
    }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name != nameof(IUserRepository.GetNamesByIdsAsync))
            throw new InvalidOperationException("Unexpected user repository operation in audit name enrichment.");
        var ids = ((IEnumerable<int>)args![0]!).ToArray();
        NameRequests.Add(ids);
        return Task.FromResult(ids.Distinct().Where(Users.ContainsKey).ToDictionary(id => id, id => Users[id]));
    }
}

internal sealed class MemoryAuditRepository : IAuditLogRepository
{
    private readonly object _gate = new();
    public List<AuditLog> Logs { get; } = [];
    public bool FailWrites { get; set; }
    public int Offset { get; private set; }
    public int Limit { get; private set; }
    public DateTime? FromUtc { get; private set; }
    public DateTime? ToUtc { get; private set; }

    public Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        if (FailWrites) throw new InvalidOperationException("provider-secret-must-not-be-logged");
        lock (_gate) { log.Id = Logs.Count + 1; Logs.Add(log); }
        return Task.CompletedTask;
    }

    public Task<List<AuditLog>> GetSummariesAsync(int offset, int limit, AuditLogFilter filters,
        DateTime? fromUtc, DateTime? toUtcExclusive, CancellationToken cancellationToken = default)
    {
        Offset = offset; Limit = limit; FromUtc = fromUtc; ToUtc = toUtcExclusive;
        return Task.FromResult(Filter(filters, fromUtc, toUtcExclusive).OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id).Skip(offset).Take(limit + 1).ToList());
    }

    public Task<int> GetCountAsync(AuditLogFilter filters, DateTime? fromUtc, DateTime? toUtcExclusive,
        CancellationToken cancellationToken = default) => Task.FromResult(Filter(filters, fromUtc, toUtcExclusive).Count());
    public Task<AuditLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult(Logs.SingleOrDefault(x => x.Id == id));

    public Task<List<AuditLog>> GetRecentDashboardActivitiesAsync(int limit)
        => Task.FromResult(Logs
            .Where(x => x.Module is Module.Tickets or Module.Vendors or Module.Users or Module.Security)
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToList());

    public Task<AuditLogCountsResponse> GetActivityCountsAsync(AuditLogFilter filters, DateTime? fromUtc,
        DateTime? toUtcExclusive, CancellationToken cancellationToken = default)
    {
        FromUtc = fromUtc; ToUtc = toUtcExclusive;
        var logs = Filter(filters, fromUtc, toUtcExclusive).ToList();
        return Task.FromResult(new AuditLogCountsResponse
        {
            RecordedActivities = logs.Count,
            SuccessfulActions = logs.Count(x => x.Result == LogResult.Success),
            SecurityEvents = logs.Count(x => x.Module == Module.Security)
        });
    }

    private IEnumerable<AuditLog> Filter(AuditLogFilter filters, DateTime? fromUtc, DateTime? toUtc)
    {
        return Logs.Where(x => (!fromUtc.HasValue || x.Timestamp >= fromUtc) &&
            (!toUtc.HasValue || x.Timestamp < toUtc) && (!filters.UserId.HasValue || x.UserId == filters.UserId) &&
            (!filters.Role.HasValue || x.Role == filters.Role) && (!filters.Module.HasValue || x.Module == filters.Module) &&
            (filters.Modules.Length == 0 || filters.Modules.Contains(x.Module)) &&
            (filters.Results.Length == 0 || filters.Results.Contains(x.Result)) &&
            (!filters.Result.HasValue || x.Result == filters.Result) && (string.IsNullOrWhiteSpace(filters.Search) ||
                x.Action.Contains(filters.Search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                x.Details.Contains(filters.Search.Trim(), StringComparison.OrdinalIgnoreCase) ||
                x.TargetId?.Contains(filters.Search.Trim(), StringComparison.OrdinalIgnoreCase) == true));
    }
}

internal sealed class AuditHost : IAsyncDisposable
{
    public MemoryAuditRepository Repository { get; } = new();
    public IUserRepository Users { get; } = AuditUserRepositoryStub.Create();
    public WebApplication App { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;

    public static async Task<AuditHost> CreateAsync(Action<IServiceCollection>? configureServices = null)
    {
        var host = new AuditHost();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IAuditLogRepository>(host.Repository);
        builder.Services.AddSingleton<IUserRepository>(host.Users);
        builder.Services.AddScoped<IAuditLogService, AuditLogService>();
        builder.Services.AddScoped<AuditLogContext>();
        builder.Services.AddScoped<AuditLogActionFilter>();
        builder.Services.AddTransient<ExceptionHandlingMiddleware>();
        configureServices?.Invoke(builder.Services);
        builder.Services.AddControllers(options => options.Filters.AddService<AuditLogActionFilter>())
            .AddApplicationPart(typeof(AdminAuditLogController).Assembly)
            .AddApplicationPart(typeof(AuditProbeController).Assembly)
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = new SnakeCaseNamingPolicy();
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            });
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        builder.Services.AddAuthorization();
        host.App = builder.Build();
        host.App.UseRouting();
        host.App.UseMiddleware<AuditLogMiddleware>();
        host.App.UseAuthentication();
        host.App.UseAuthorization();
        host.App.UseMiddleware<ExceptionHandlingMiddleware>();
        host.App.MapControllers();
        await host.App.StartAsync();
        host.Client = host.App.GetTestClient();
        return host;
    }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route,
        string? role = "HeadAdmin", string? body = null)
    {
        using var request = new HttpRequestMessage(method, route);
        if (role is not null) request.Headers.Add("X-Test-Role", role);
        if (body is not null) request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        return await Client.SendAsync(request);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.DisposeAsync();
    }
}

internal sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].FirstOrDefault();
        if (role is null) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "7"),
            new Claim("role", role)], Scheme.Name, ClaimTypes.NameIdentifier, "role");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

[ApiController]
[Route("api/audit-probe")]
[Authorize(Roles = "HeadAdmin,AdminOfficer")]
public class AuditProbeController : ControllerBase
{
    private readonly AuditLogContext _audit;
    private readonly IAuditLogService _service;
    public AuditProbeController(AuditLogContext audit, IAuditLogService service) { _audit = audit; _service = service; }

    [HttpPost("mutate")]
    public IActionResult Mutate([FromBody] AuditProbeRequest request) => Ok(new { Id = 42 });
    [HttpGet("read")]
    public IActionResult Read() => Ok(new { Id = 42 });
    [HttpPost("read-only")]
    public IActionResult GetTicketFineSummary() => Ok(new { Id = 42 });
    [HttpPost("validation-exception")]
    public IActionResult Invalid() => throw new ValidationException("sensitive-input-must-not-be-stored");
    [HttpPost("business-failure")]
    public IActionResult Failed() => throw new InvalidCredentialsException("sensitive-password-must-not-be-stored");
    [HttpPost("business-failure-200")]
    public IActionResult FailedWithOk() => Ok(new ResetPasswordResponse { Success = false, Message = "sensitive-reset-token" });
    [HttpPost("password-failure")]
    public IActionResult PasswordFailure() => BadRequest(new { code = "PASSWORD_CHANGE_FAILED", message = "sensitive-password" });
    [HttpPost("unexpected-error")]
    public IActionResult Error() => throw new InvalidOperationException("sensitive-provider-secret");
    [HttpPost("no-content")]
    public IActionResult Update() => NoContent();
    [HttpPost("committed")]
    public async Task<IActionResult> Committed()
    {
        _audit.SaveWithTransaction = true;
        _audit.Entry!.TargetId = "42";
        await _service.StageCurrentAsync();
        _audit.Stored = true;
        return Ok(new { Id = 42 });
    }
    [HttpPost("after-commit-error")]
    public async Task<IActionResult> AfterCommitError()
    {
        await Committed();
        throw new InvalidOperationException("sensitive-post-commit-error");
    }
}

public class AuditProbeRequest
{
    [Required]
    public string? Name { get; set; }
    public int UserId { get; set; }
    public string? Role { get; set; }
    public string? Password { get; set; }
}
