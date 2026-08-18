namespace MarikinaMarket.API.Domain.Entities
{
    public class UserDeviceToken
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public required string DeviceToken { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime LastUsedAt { get; set; }
    }
}