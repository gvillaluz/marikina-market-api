using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class MarketSectionConfiguration : IEntityTypeConfiguration<MarketSection>
    {
        public void Configure(EntityTypeBuilder<MarketSection> builder)
        {
            builder.Property(x => x.Name)
                .HasMaxLength(150);

            builder.HasIndex(x => x.Name)
                .IsUnique();

            builder.Property(x => x.Description)
                .HasMaxLength(500);

            builder.Property(x => x.CreatedAt)
                .HasDefaultValueSql("timezone('utc', now())");
        }
    }
}
