using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.DTOs.Audits.Internal
{
    // One request shares this context with its business services and transaction.
    public class AuditLogContext
    {
        public AuditLog? Entry { get; set; }
        public bool IsMutation { get; set; }
        public bool IsValidationFailure { get; set; }
        public bool IsBusinessFailure { get; set; }
        public bool IsSecurityFailure { get; set; }
        public bool ActorResolved { get; set; }
        public string PerformerDescription => Entry is null ? "System" : Entry.UserId.HasValue ? "User" : "Anonymous";
        public bool SaveWithTransaction { get; set; }
        public bool Staged { get; set; }
        public bool Stored { get; set; }
    }
}
