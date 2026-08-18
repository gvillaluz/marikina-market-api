using FirebaseAdmin.Messaging;
using MarikinaMarket.API.Application.Interfaces.Services;

namespace MarikinaMarket.API.Application.Services
{
    public class PushNotificationService : IPushNotificationService
    {
        public async Task<bool> SendAsync(string deviceToken, string title, string body, Dictionary<string, string>? data = null)
        {
            try
            {
                var message = new Message()
                {
                    Token = deviceToken,
                    Notification = new Notification{ Title = title, Body = body },
                    Data = data  
                };

                await FirebaseMessaging.DefaultInstance.SendAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to send notification: " + ex);
                return false;
            }
        }
    }
}