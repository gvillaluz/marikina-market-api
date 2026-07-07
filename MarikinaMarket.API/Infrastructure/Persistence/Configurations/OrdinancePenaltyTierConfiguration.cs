using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class OrdinancePenaltyTierConfiguration : IEntityTypeConfiguration<OrdinancePenaltyTier>
    {
        public void Configure(EntityTypeBuilder<OrdinancePenaltyTier> builder)
        {
            builder.HasIndex(t => new { t.OrdinanceId, t.OffenseNumber })
                .IsUnique();

            builder.HasOne(t => t.Ordinance)
                .WithMany(o => o.PenaltyTiers)
                .HasForeignKey(t => t.OrdinanceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(t => t.OffenseNumber)
                .IsRequired();

            builder.Property(t => t.Severity)
                .HasConversion<string>();

            builder.Property(t => t.PenaltyAmount)
                .HasPrecision(18, 2);
        }
    }
}
