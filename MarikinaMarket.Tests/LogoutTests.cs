using System.ComponentModel.DataAnnotations;
using System.Reflection;
using MarikinaMarket.API.Application.DTOs.Auth.Request;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Presentation.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace MarikinaMarket.Tests;

public class LogoutTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("mobile-refresh-token")]
    public async Task Logout_only_removes_supplied_tokens_and_confirms_repeated_requests(string? refreshToken)
    {
        var calls = new List<string>();
        var service = CreateService(calls);
        var request = new LogoutRequest { DeviceToken = "this-device", RefreshToken = refreshToken };

        for (var i = 0; i < 2; i++)
        {
            var response = await service.LogoutAsync(request);
            Assert.Equal("Logout confirmed.", response.Message);
        }

        string[] expected = refreshToken is null
            ? ["BeginTransactionAsync", "DeleteDeviceTokenAsync:this-device", "CommitAsync"]
            : ["BeginTransactionAsync", "DeleteDeviceTokenAsync:this-device", "RevokeRefreshTokenAsync:mobile-refresh-token", "CommitAsync"];
        Assert.Equal(expected.Concat(expected), calls);
    }

    [Theory]
    [InlineData("", null)]
    [InlineData(" ", null)]
    [InlineData("device", "")]
    [InlineData("device", " ")]
    [InlineData("device", "has whitespace")]
    public async Task Invalid_tokens_are_rejected_before_writes(string deviceToken, string? refreshToken)
    {
        var calls = new List<string>();
        await Assert.ThrowsAsync<ValidationException>(() => CreateService(calls).LogoutAsync(
            new LogoutRequest { DeviceToken = deviceToken, RefreshToken = refreshToken }));
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Oversized_tokens_are_rejected_before_writes()
    {
        var calls = new List<string>();
        var service = CreateService(calls);
        await Assert.ThrowsAsync<ValidationException>(() => service.LogoutAsync(
            new LogoutRequest { DeviceToken = new string('x', 501) }));
        await Assert.ThrowsAsync<ValidationException>(() => service.LogoutAsync(
            new LogoutRequest { DeviceToken = "device", RefreshToken = new string('x', 89) }));
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Mobile_logout_rolls_back_when_revocation_fails()
    {
        var calls = new List<string>();
        var service = CreateService(calls, (method, _) =>
        {
            if (method.Name == nameof(IUserRepository.RevokeRefreshTokenAsync))
                throw new InvalidOperationException("Simulated failure");
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LogoutAsync(
            new LogoutRequest { DeviceToken = "device", RefreshToken = "refresh" }));
        Assert.Equal(["BeginTransactionAsync", "DeleteDeviceTokenAsync:device", "RevokeRefreshTokenAsync:refresh", "RollbackAsync"], calls);
    }

    [Fact]
    public async Task Cleanup_uses_one_utc_cutoff_and_forwards_cancellation()
    {
        var calls = new List<string>();
        using var cancellation = new CancellationTokenSource();
        DateTime? cutoff = null;
        var service = CreateService(calls, (method, args) =>
        {
            if (method.Name != nameof(IUserRepository.DeleteOldTokensAsync)) return;
            cutoff = (DateTime)args![0]!;
            Assert.Equal(cancellation.Token, args[1]);
        });
        var earliest = DateTime.UtcNow.AddDays(-60);
        await service.CleanupTokensAsync(cancellation.Token);
        Assert.NotNull(cutoff);
        Assert.Equal(DateTimeKind.Utc, cutoff.Value.Kind);
        Assert.InRange(cutoff.Value, earliest, DateTime.UtcNow.AddDays(-60));
        Assert.Equal(["BeginTransactionAsync", "DeleteOldTokensAsync", "CommitAsync"], calls);
    }

    [Fact]
    public async Task Cleanup_rolls_back_when_deletion_fails()
    {
        var calls = new List<string>();
        var service = CreateService(calls, (_, _) => throw new InvalidOperationException("Simulated failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CleanupTokensAsync());
        Assert.Equal(["BeginTransactionAsync", "DeleteOldTokensAsync", "RollbackAsync"], calls);
    }

    [Fact]
    public void Logout_allows_anonymous_requests()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Logout))!;
        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    private static AuthService CreateService(List<string> calls, Action<MethodInfo, object?[]?>? repositoryAction = null)
    {
        var repository = Proxy<IUserRepository>((method, args) =>
        {
            calls.Add(method.Name + (args![0] is string token ? ":" + token : ""));
            repositoryAction?.Invoke(method, args);
            return Task.CompletedTask;
        });
        var unitOfWork = Proxy<IUnitOfWork>((method, _) =>
        {
            calls.Add(method.Name);
            return Task.CompletedTask;
        });
        return new AuthService(repository, null!, unitOfWork, null!, null!);
    }

    private static T Proxy<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, LogoutProxy>();
        ((LogoutProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class LogoutProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args);
    }
}
