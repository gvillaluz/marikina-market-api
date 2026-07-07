using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class TicketViolationConfiguration : IEntityTypeConfiguration<TicketViolation>
    {
        public void Configure(EntityTypeBuilder<TicketViolation> builder)
        {
            builder.HasIndex(tv => new { tv.TicketId, tv.OrdinanceId })
                .IsUnique();

            builder.HasOne(t => t.Ticket)
                .WithMany(tv => tv.TicketViolations)
                .HasForeignKey(tv => tv.TicketId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(t => t.Ordinance)
                .WithMany(tv => tv.TicketViolations)
                .HasForeignKey(tv => tv.OrdinanceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(tv => tv.PenaltyAmount)
                .HasPrecision(18, 2);

            builder.Property(tv => tv.OffenseCount)
                .IsRequired();
        }
    }
}
