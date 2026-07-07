using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class OrdinanceConfiguration : IEntityTypeConfiguration<Ordinance>
    {
        public void Configure(EntityTypeBuilder<Ordinance> builder)
        {
            builder.Property(x => x.OrdinanceNo)
                .HasMaxLength(100);

            builder.HasIndex(x => x.OrdinanceNo)
                .IsUnique();

            builder.Property(x => x.Title)
                .HasMaxLength(250);

            builder.Property(x => x.Description)
                .HasMaxLength(1000);

            builder.Property(x => x.Category)
                .HasConversion<string>();

            builder.Property(x => x.CreatedAt)
                .HasDefaultValueSql("timezone('utc', now())");
        }
    }
}
