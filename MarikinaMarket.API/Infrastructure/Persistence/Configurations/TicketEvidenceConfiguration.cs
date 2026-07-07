using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class TicketEvidenceConfiguration : IEntityTypeConfiguration<TicketEvidence>
    {
        public void Configure(EntityTypeBuilder<TicketEvidence> builder)
        {
            builder.HasOne(t => t.Ticket)
                .WithMany(t => t.TicketEvidences)
                .HasForeignKey(t => t.TicketId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(t => t.EvidenceUrl)
                .HasMaxLength(1000);
        }
    }
}
