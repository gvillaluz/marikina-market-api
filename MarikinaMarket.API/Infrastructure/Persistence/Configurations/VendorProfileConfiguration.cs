using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class VendorProfileConfiguration : IEntityTypeConfiguration<VendorProfile>
    {
        public void Configure(EntityTypeBuilder<VendorProfile> builder)
        {
            builder.Property(v => v.StallNumber)
                .HasMaxLength(20);

            builder.HasIndex(v => new
            {
                v.MarketSectionId, 
                v.StallNumber
            }).IsUnique();

            builder.HasOne(u => u.User)
                .WithOne(v => v.VendorProfile)
                .HasForeignKey<VendorProfile>(v => v.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(m => m.MarketSection)
                .WithMany(v => v.VendorProfiles)
                .HasForeignKey(v => v.MarketSectionId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(v => v.BusinessName)
                .HasMaxLength(100);

            builder.HasIndex(v => v.QrCodeValue)
                .IsUnique();

            builder.Property(v => v.Status)
                .HasConversion<string>();

            builder.Property(v => v.RegisteredAt)
                .HasDefaultValueSql("timezone('utc', now())");

            builder.Property(v => v.ScoreUpdatedAt)
                .HasDefaultValueSql("timezone('utc', now())");
        }
    }
}
