namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body, string accentColor = "#0B2D5B");
    }
}
