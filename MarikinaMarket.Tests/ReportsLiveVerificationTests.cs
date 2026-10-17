using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using MarikinaMarket.API.Application.Interfaces;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Infrastructure.Json;
using MarikinaMarket.API.Infrastructure.BackgroundServices;
using MarikinaMarket.API.Infrastructure.Persistence;
using MarikinaMarket.API.Infrastructure.Repositories;
using MarikinaMarket.API.Presentation.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;
using System.Security.Claims;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using Xunit;
namespace MarikinaMarket.Tests;
public partial class ReportsLiveVerificationTests {
private static async Task<WebApplication> StartHost(string source, string connection, Mailbox mailbox) {var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(ReportService).Assembly.FullName, ContentRootPath = source, EnvironmentName = "Development" });

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenLocalhost(5076);
    
});

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
    ["ConnectionStrings:DefaultConnection"] = connection,
    ["Jwt:Secret"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)),
    ["Jwt:Issuer"] = "reports-local", ["Jwt:Audience"] = "reports-local",
    ["Cors:AllowedOrigins:0"] = "http://localhost:5176"
});
builder.Logging.ClearProviders();
builder.Services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
            .UseSnakeCaseNamingConvention();
});

builder.Services.AddIdentityCore<User>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireLowercase = false;
})
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();



builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<AuditLogContext>();
builder.Services.AddScoped<AuditLogActionFilter>();
builder.Services.AddScoped<IVendorRepository, VendorRepository>();
builder.Services.AddScoped<ITicketRepository, TicketRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IOrdinanceRepository, OrdinanceRepository>();
builder.Services.AddScoped<IMarketSectionRepository, MarketSectionRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IOtpRepository, OtpRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IBackupRepository, BackupRepository>();

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<IVendorService, VendorService>();
builder.Services.AddScoped<IVendorComplianceScoreService, VendorComplianceScoreService>();
builder.Services.AddScoped<IAdminAnalyticsService, AdminAnalyticsService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IOrdinanceService, OrdinanceService>();
builder.Services.AddScoped<IMarketSectionService, MarketSectionService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<IEmailService>(mailbox);
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEnforcerService, EnforcerService>();
builder.Services.AddScoped<IStorageService, StorageService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<IBackupService, BackupService>();




builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var secretKey = builder.Configuration["Jwt:Secret"]
        ?? throw new InvalidOperationException("JWT Secret is missing.");

    options.MapInboundClaims = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero,
        RoleClaimType = "role"
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var action = context.HttpContext.GetEndpoint()?.Metadata
                .GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>();
            if (action?.ControllerTypeInfo.AsType() == typeof(MarikinaMarket.API.Presentation.Controllers.AuthController)
                && action.ActionName == nameof(MarikinaMarket.API.Presentation.Controllers.AuthController.Logout))
                context.NoResult();
            return Task.CompletedTask;
        },
        OnTokenValidated = async context =>
        {
            var userIdValue = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var securityStamp = context.Principal?.FindFirst("security_stamp")?.Value;
            if (!int.TryParse(userIdValue, out var userId) || userId <= 0 || string.IsNullOrEmpty(securityStamp))
            {
                context.Fail("Invalid session. Please log in again.");
                return;
            }

            var repository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var user = await repository.GetUserAsync(userId);
            if (user is null || !string.Equals(user.SecurityStamp, securityStamp, StringComparison.Ordinal))
                context.Fail("Session expired. Please log in again.");
        }
    };
});

builder.Services.AddControllers(options => options.Filters.AddService<AuditLogActionFilter>())
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = new SnakeCaseNamingPolicy();
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    options.AddPolicy("ConfiguredOrigins", policy =>
    {
        policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ExceptionHandlingMiddleware>();

var app = builder.Build();

app.UseRouting();
app.UseCors("ConfiguredOrigins");
app.UseMiddleware<AuditLogMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    await next();

    if (context.Response.StatusCode == StatusCodes.Status404NotFound && !context.Response.HasStarted)
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message = "The requested resource was not found." });
    }
});

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MarikinaMarket API v1");
    });
}

if (!app.Environment.IsDevelopment()) {
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.MapControllers();
await app.StartAsync(); return app;
}
private sealed class Mailbox : IEmailService {
    public ConcurrentDictionary<string, string> Codes { get; } = new();
    public Task SendEmailAsync(string toEmail, string subject, string body, string accentColor = "#0B2D5B") {
        if (!toEmail.EndsWith("@example.invalid")) throw new InvalidOperationException("External mail prohibited.");
        var match = Regex.Match(body, @">([0-9]{6})</strong>");
        if (!match.Success) throw new InvalidOperationException("OTP mailbox format changed.");
        Codes[toEmail] = match.Groups[1].Value;
        return Task.CompletedTask;
    }
}
}
