namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface INotificationService
    {
        Task<bool> SendPushNotificationAsync(int userId, string title, string body, Dictionary<string, string>? data = null);
        Task SendEmailAsync(string toEmail, string subject, string body);
    }
}