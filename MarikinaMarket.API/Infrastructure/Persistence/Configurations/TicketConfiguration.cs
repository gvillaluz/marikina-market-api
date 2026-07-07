using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
    {
        public void Configure(EntityTypeBuilder<Ticket> builder)
        {
            builder.Property(x => x.PrimaryCategory)
                .HasConversion<string>();

            builder.HasIndex(x => new { x.VendorId, x.PrimaryCategory, x.IssuedAt });

            builder.HasIndex(x => x.ControlNumber)
                .IsUnique();

            builder.HasOne(x => x.Vendor)
                .WithMany(t => t.Tickets)
                .HasForeignKey(x => x.VendorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Enforcer)
                .WithMany(t => t.IssuedTickets)
                .HasForeignKey(x => x.EnforcerId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.MarketSection)
                .WithMany(t => t.Tickets)
                .HasForeignKey(x => x.MarketSectionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(x => x.Description)
                .HasMaxLength(1000);

            builder.Property(x => x.TotalPaymentAmount)
                .HasPrecision(18, 2);

            builder.Property(x => x.PaymentStatus)
                .HasConversion<string>();

            builder.Property(x => x.ReceiptUrl)
                .HasMaxLength(1000);

            builder.Property(x => x.HighestSeverity)
                .HasConversion<string>();

            builder.Property(t => t.Status)
                .HasConversion<string>();

            builder.Property(t => t.Type)
                .HasConversion<string>();

            builder.Property(t => t.PenaltyType)
                .HasConversion<string>();

            builder.Property(v => v.IssuedAt)
                .HasDefaultValueSql("timezone('utc', now())");
        }
    }
}
