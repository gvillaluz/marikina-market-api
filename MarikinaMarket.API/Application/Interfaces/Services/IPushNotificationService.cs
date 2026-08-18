namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IPushNotificationService
    {
        Task<bool> SendAsync(string deviceToken, string title, string body, Dictionary<string, string>? data = null);
    }
}