using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;

namespace MarikinaMarket.API.Application.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IEmailService _emailService;
        private readonly IUserRepository _userRepository;

        public NotificationService(
            IPushNotificationService pushNotificationService,
            IEmailService emailService,
            IUserRepository userRepository
            )
        {
            _pushNotificationService = pushNotificationService;
            _emailService = emailService;
            _userRepository = userRepository;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            await _emailService.SendEmailAsync(toEmail, subject, body);
        }

        public async Task<bool> SendPushNotificationAsync(int userId, string title, string body, Dictionary<string, string>? data = null)
        {
            var deviceTokens = await _userRepository.GetDeviceTokensByIdAsync(userId);

            if (deviceTokens is null || deviceTokens.Count == 0)
                return false;

            var anySucceeded = false;

            foreach (var token in deviceTokens)
            {
                var success = await _pushNotificationService.SendAsync(token, title, body, data);
                if (success)
                    anySucceeded = true;
            }

            return anySucceeded;
        }
    }
}