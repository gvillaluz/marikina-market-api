using System.Reflection;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Presentation.Controllers;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace MarikinaMarket.Tests;

public class AdminPushNotificationTests
{
    [Fact]
    public async Task Admin_push_sends_to_registered_tokens_without_using_notification_repository()
    {
        var users = DispatchProxy.Create<IUserRepository, AdminTokenRepositoryStub>();
        var userStub = (AdminTokenRepositoryStub)(object)users;
        userStub.Tokens = ["browser-token-1", "browser-token-2"];
        var push = DispatchProxy.Create<IPushNotificationService, PushSenderStub>();
        var pushStub = (PushSenderStub)(object)push;
        var service = new NotificationService(push, null!, users, null!);

        var sent = await service.SendPushNotificationToAdminsAsync(
            "New vendor registration request", "Business submitted a request.",
            new Dictionary<string, string> { ["type"] = "vendor_registration", ["registration_id"] = "23" });

        Assert.True(sent);
        Assert.Equal(userStub.Tokens, pushStub.Tokens);
        Assert.All(pushStub.Titles, title => Assert.Equal("New vendor registration request", title));
    }

    [Fact]
    public async Task Admin_push_returns_false_when_no_admin_browser_tokens_are_registered()
    {
        var users = DispatchProxy.Create<IUserRepository, AdminTokenRepositoryStub>();
        var push = DispatchProxy.Create<IPushNotificationService, PushSenderStub>();
        var pushStub = (PushSenderStub)(object)push;
        var service = new NotificationService(push, null!, users, null!);

        var sent = await service.SendPushNotificationToAdminsAsync("title", "body");

        Assert.False(sent);
        Assert.Empty(pushStub.Tokens);
    }

    [Fact]
    public void Device_token_endpoint_allows_admins_and_enforcers()
    {
        var method = typeof(UserController).GetMethod(nameof(UserController.RegisterDeviceToken))!;
        var authorize = method.GetCustomAttribute<AuthorizeAttribute>();
        Assert.Equal("HeadAdmin,AdminOfficer,MarketEnforcer", authorize?.Roles);
    }

    public class AdminTokenRepositoryStub : DispatchProxy
    {
        public List<string> Tokens { get; set; } = [];

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Assert.Equal(nameof(IUserRepository.GetAdminDeviceTokensAsync), method?.Name);
            return Task.FromResult(Tokens);
        }
    }

    public class PushSenderStub : DispatchProxy
    {
        public List<string> Tokens { get; } = [];
        public List<string> Titles { get; } = [];

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Assert.Equal(nameof(IPushNotificationService.SendAsync), method?.Name);
            Tokens.Add((string)args![0]!);
            Titles.Add((string)args[1]!);
            return Task.FromResult(true);
        }
    }
}
