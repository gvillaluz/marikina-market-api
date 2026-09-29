using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Domain.Entities
{
    public class OtpVerification
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public required string CodeHash { get; set; }
        public OtpPurpose Purpose { get; set; }
        public DateTime ExpiresAt { get; set; }
        public int Attempts { get; set; }
        public bool IsUsed { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}