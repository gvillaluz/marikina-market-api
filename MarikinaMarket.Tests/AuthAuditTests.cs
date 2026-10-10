using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Module = MarikinaMarket.API.Domain.Enums.Module;

namespace MarikinaMarket.Tests;

public class AuthAuditTests
{
    private const string LoginBody = "{\"username\":\"user8\",\"password\":\"valid-password-123\"}";
    private const string VerificationBody = "{\"username\":\"user8\",\"password\":\"valid-password-123\",\"code\":\"123456\"}";

    [Theory]
    [InlineData("login", Role.MarketVendor, "Login")]
    [InlineData("login-mobile", Role.MarketEnforcer, "LoginMobile")]
    [InlineData("verify-login", Role.MarketVendor, "VerifyLogin")]
    [InlineData("verify-login-mobile", Role.MarketEnforcer, "VerifyLoginMobile")]
    public async Task Login_and_verification_record_the_user_without_a_bearer_token(string route, Role role, string action)
    {
        var fixture = new AuthAuditFixture(role);
        await using var host = await fixture.HostAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/api/Auth/" + route, null,
            route.StartsWith("verify", StringComparison.Ordinal) ? VerificationBody : LoginBody);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var log = Assert.Single(host.Repository.Logs);
        Assert.Equal(action, log.Action);
        Assert.Equal(8, log.UserId);
        Assert.Equal(role, log.Role);
        Assert.Equal("8", log.TargetId);
        Assert.Equal(LogResult.Success, log.Result);
        Assert.DoesNotContain("Anonymous", log.Details);
        Assert.DoesNotContain("valid-password", JsonSerializer.Serialize(log));
        if (!route.StartsWith("verify", StringComparison.Ordinal))
            Assert.Equal(8, Assert.Single(fixture.EmailActors).UserId);
    }

    [Theory]
    [InlineData("login")]
    [InlineData("verify-login")]
    public async Task A_different_authenticated_request_identity_does_not_overwrite_the_login_actor(string route)
    {
        var fixture = new AuthAuditFixture();
        await using var host = await fixture.HostAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/api/Auth/" + route, "HeadAdmin",
            route == "login" ? LoginBody : VerificationBody);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var log = Assert.Single(host.Repository.Logs);
        Assert.Equal(8, log.UserId);
        Assert.Equal(Role.MarketVendor, log.Role);
    }

    [Theory]
    [InlineData("verify-login", Role.MarketVendor, OtpVerificationResult.InvalidCode)]
    [InlineData("verify-login", Role.MarketVendor, OtpVerificationResult.Expired)]
    [InlineData("verify-login", Role.MarketVendor, OtpVerificationResult.TooManyAttempts)]
    [InlineData("verify-login", Role.MarketVendor, OtpVerificationResult.NotFound)]
    [InlineData("verify-login-mobile", Role.MarketEnforcer, OtpVerificationResult.InvalidCode)]
    public async Task Failed_OTP_authentication_is_logged_for_the_password_validated_user_without_issuing_a_token(
        string route, Role role, OtpVerificationResult result)
    {
        var fixture = new AuthAuditFixture(role) { OtpResult = result };
        await using var host = await fixture.HostAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/api/Auth/" + route, null, VerificationBody);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var log = Assert.Single(host.Repository.Logs);
        Assert.Equal(8, log.UserId);
        Assert.Equal(role, log.Role);
        Assert.Equal(LogResult.Failed, log.Result);
        Assert.Equal(Module.Security, log.Module);
        Assert.Equal(0, fixture.TokensIssued);
        Assert.DoesNotContain("123456", JsonSerializer.Serialize(log));
    }

    [Theory]
    [InlineData("login")]
    [InlineData("verify-login")]
    public async Task Invalid_credentials_do_not_claim_the_target_account_as_the_actor(string route)
    {
        var fixture = new AuthAuditFixture();
        await using var host = await fixture.HostAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/api/Auth/" + route, null,
            (route == "login" ? LoginBody : VerificationBody).Replace("valid-password-123", "incorrect-password"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var log = Assert.Single(host.Repository.Logs);
        Assert.Null(log.UserId);
        Assert.Null(log.Role);
        Assert.Equal("8", log.TargetId);
        Assert.Contains("Anonymous", log.Details);
        Assert.DoesNotContain("System", log.Details);
        Assert.Equal(0, fixture.TokensIssued);
    }

    [Theory]
    [InlineData("login", "{\"username\":\"user8\",\"password\":\"short\"}")]
    [InlineData("verify-login", "{\"username\":\"user8\",\"password\":\"valid-password-123\",\"code\":\"bad\"}")]
    [InlineData("verify-otp", "{\"username\":\"user8\",\"code\":\"bad\"}")]
    public async Task Malformed_input_still_produces_no_audit_record(string route, string body)
    {
        await using var host = await new AuthAuditFixture().HostAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/api/Auth/" + route, null, body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(host.Repository.Logs);
    }

    [Theory]
    [InlineData(OtpVerificationResult.Success, 8)]
    [InlineData(OtpVerificationResult.InvalidCode, null)]
    public async Task Reset_OTP_verification_requires_proof_before_assigning_the_account_actor(
        OtpVerificationResult result, int? actor)
    {
        var fixture = new AuthAuditFixture() { OtpResult = result };
        await using var host = await fixture.HostAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/api/Auth/verify-otp", null,
            "{\"username\":\"user8\",\"code\":\"123456\"}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var log = Assert.Single(host.Repository.Logs);
        Assert.Equal(actor, log.UserId);
        Assert.Equal(actor.HasValue ? Role.MarketVendor : null, log.Role);
        Assert.Equal("8", log.TargetId);
        Assert.Equal(actor.HasValue ? LogResult.Success : LogResult.Failed, log.Result);
        Assert.DoesNotContain("opaque-reset-token", JsonSerializer.Serialize(log));
    }

    [Fact]
    public async Task Reset_code_request_by_username_only_remains_anonymous_and_records_its_target()
    {
        var fixture = new AuthAuditFixture();
        await using var host = await fixture.HostAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/api/Auth/send-otp", null,
            "{\"username\":\"user8\",\"channel\":\"email\"}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var log = Assert.Single(host.Repository.Logs);
        Assert.Null(log.UserId);
        Assert.Null(log.Role);
        Assert.Equal("8", log.TargetId);
        Assert.Contains("Anonymous", log.Details);
        Assert.Null(Assert.Single(fixture.EmailActors).UserId);
    }

    [Theory]
    [InlineData("refresh-web", Role.MarketVendor, 200)]
    [InlineData("refresh-web", Role.MarketEnforcer, 403)]
    [InlineData("refresh", Role.MarketEnforcer, 200)]
    public async Task Refresh_endpoints_do_not_record_audit_entries(string route, Role role, int status)
    {
        var fixture = new AuthAuditFixture(role);
        await using var host = await fixture.HostAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/api/Auth/" + route, "HeadAdmin",
            route == "refresh" ? "{\"refresh_token\":\"opaque-refresh-token\"}" : "{\"access_token\":\"opaque-access-token\"}");
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Empty(host.Repository.Logs);
    }

    [Fact]
    public async Task Email_audit_inherits_the_login_user_even_when_delivery_fails_before_network_access()
    {
        var fixture = new AuthAuditFixture() { UseActualEmailService = true };
        await using var host = await fixture.HostAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/api/Auth/login", "HeadAdmin", LoginBody);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(2, host.Repository.Logs.Count);
        Assert.All(host.Repository.Logs, log =>
        {
            Assert.Equal(8, log.UserId);
            Assert.Equal(Role.MarketVendor, log.Role);
            Assert.Equal(LogResult.Failed, log.Result);
        });
        var email = Assert.Single(host.Repository.Logs, log => log.Action == "SendEmail");
        Assert.StartsWith("User email delivery", email.Details);
    }

    [Theory]
    [InlineData(true, "System")]
    [InlineData(false, "Anonymous")]
    public async Task Delivery_audits_distinguish_system_jobs_from_unresolved_requests(bool system, string label)
    {
        await using var host = await AuditHost.CreateAsync();
        using var scope = host.App.Services.CreateScope();
        var audit = scope.ServiceProvider.GetRequiredService<AuditLogContext>();
        if (!system) audit.Entry = new AuditLog { Action = "SendOtpCode", Module = Module.Security, Details = "Request." };
        var email = new EmailService(new ConfigurationBuilder().Build(), scope.ServiceProvider.GetRequiredService<IAuditLogService>(), audit);
        await Assert.ThrowsAsync<InvalidOperationException>(() => email.SendEmailAsync("test@example.com", "subject", "body"));
        var log = Assert.Single(host.Repository.Logs);
        Assert.Null(log.UserId);
        Assert.Null(log.Role);
        Assert.StartsWith(label + " email delivery", log.Details);
    }

    [Theory]
    [InlineData("opaque-reset-token", 8, LogResult.Success)]
    [InlineData("invalid-reset-token", null, LogResult.Failed)]
    public async Task Password_reset_assigns_the_account_actor_only_after_the_reset_token_is_verified(
        string token, int? actor, LogResult result)
    {
        var fixture = new AuthAuditFixture();
        await using var host = await fixture.HostAsync();
        var response = await host.SendAsync(HttpMethod.Post, "/api/Auth/reset-password", null,
            "{\"username\":\"user8\",\"new_password\":\"updated-password-123\",\"reset_token\":\"" + token + "\"}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var log = Assert.Single(host.Repository.Logs);
        Assert.Equal(actor, log.UserId);
        Assert.Equal("8", log.TargetId);
        Assert.Equal(result, log.Result);
        Assert.DoesNotContain(token, JsonSerializer.Serialize(log));
        Assert.DoesNotContain("updated-password", JsonSerializer.Serialize(log));
    }
}

internal sealed class AuthAuditFixture
{
    public Role Role { get; }
    public OtpVerificationResult OtpResult { get; set; } = OtpVerificationResult.Success;
    public bool UseActualEmailService { get; set; }
    public int TokensIssued { get; private set; }
    public List<(int? UserId, Role? Role)> EmailActors { get; } = [];
    private bool _generated;
    private readonly User _user = new()
    {
        Id = 8, UserName = "user8", FirstName = "Maria", LastName = "Santos", Email = "user8@example.com",
        DateOfBirth = new(1995, 1, 1), HouseNumber = "12", Street = "Rizal", Barangay = "Sto. Nino", City = "Marikina",
        SecurityStamp = "user-8-stamp", Status = AccountStatus.Active
    };

    public AuthAuditFixture(Role role = Role.MarketVendor) => Role = role;

    public Task<AuditHost> HostAsync() => AuditHost.CreateAsync(services =>
    {
        services.AddSingleton(Proxy<IUserRepository>((method, args) => method.Name switch
        {
            "FindByUserNameAsync" => Task.FromResult<User?>((string)args![0]! == _user.UserName ? _user : null),
            "GetUserAsync" => Task.FromResult<User?>((int)args![0]! == _user.Id ? _user : null),
            "CheckPasswordAsync" => Task.FromResult((string)args![1]! == "valid-password-123" ? SignInResult.Success : SignInResult.Failed),
            "GetRoleAsync" => Task.FromResult<Role?>(Role),
            "GenerateResetPassTokenAsync" => Task.FromResult("opaque-reset-token"),
            "ResetPasswordByUsernameAsync" => Task.FromResult((string)args![1]! == "opaque-reset-token"
                ? IdentityResult.Success : IdentityResult.Failed(new IdentityError { Code = "InvalidToken" })),
            "GetNamesByIdsAsync" => Task.FromResult(new Dictionary<int, UserNamesResponse>
            {
                [8] = new() { Id = 8, FirstName = "Maria", LastName = "Santos" }
            }),
            "AddRefreshTokenAsync" => Task.FromResult((RefreshToken)args![0]!),
            "SaveChangesAsync" => Task.CompletedTask,
            "GetRefreshTokenAsync" => Task.FromResult<RefreshToken?>(new RefreshToken
            {
                Token = "opaque-refresh-token", UserId = 8, User = _user, ExpiresAt = DateTime.UtcNow.AddDays(60)
            }),
            _ => throw new InvalidOperationException("Unexpected authentication repository call: " + method.Name)
        }));
        services.AddSingleton(Proxy<IOtpService>((method, args) => method.Name switch
        {
            "GetLatestOtpAsync" => Task.FromResult<OtpVerification?>(_generated ? new OtpVerification
            {
                Id = 100, UserId = 8, CodeHash = "test-hash", Purpose = (OtpPurpose)args![1]!,
                CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            } : null),
            "GenerateOtpAsync" => GenerateOtp(),
            "ValidateOtpAsync" => Task.FromResult(OtpResult),
            _ => throw new InvalidOperationException("Unexpected OTP operation.")
        }));
        services.AddSingleton(Proxy<ITokenService>((method, args) => method.Name switch
        {
            "GenerateAccessToken" => GenerateToken(),
            "GenerateRefreshToken" => new RefreshToken { Token = "opaque-refresh-token", UserId = 8, ExpiresAt = DateTime.UtcNow.AddDays(60) },
            "ValidateAccessTokenAsync" => Task.FromResult<ClaimsPrincipal?>(new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, "8"), new Claim("security_stamp", _user.SecurityStamp!)], "validated-token"))),
            _ => throw new InvalidOperationException("Unexpected token operation.")
        }));
        services.AddSingleton(Proxy<IUnitOfWork>((method, args) => method.Name == "SaveChangesAsync" ? Task.FromResult(1) : Task.CompletedTask));
        if (UseActualEmailService)
        {
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddScoped<IEmailService, EmailService>();
        }
        else services.AddScoped<IEmailService>(provider => new AuditEmailCapture(provider.GetRequiredService<AuditLogContext>(), EmailActors));
        services.AddScoped<IAuthService, AuthService>();
    });

    private Task<string> GenerateOtp() { _generated = true; return Task.FromResult("123456"); }
    private Task<string> GenerateToken() { TokensIssued++; return Task.FromResult("opaque-access-token"); }
    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, AuthAuditProxy>();
        ((AuthAuditProxy)(object)proxy).Handler = handler;
        return proxy;
    }
}

public class AuthAuditProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args);
}

internal sealed class AuditEmailCapture : IEmailService
{
    private readonly AuditLogContext _audit;
    private readonly List<(int? UserId, Role? Role)> _actors;
    public AuditEmailCapture(AuditLogContext audit, List<(int?, Role?)> actors) { _audit = audit; _actors = actors; }
    public Task SendEmailAsync(string toEmail, string subject, string body)
    {
        _actors.Add((_audit.Entry?.UserId, _audit.Entry?.Role));
        return Task.CompletedTask;
    }
}
