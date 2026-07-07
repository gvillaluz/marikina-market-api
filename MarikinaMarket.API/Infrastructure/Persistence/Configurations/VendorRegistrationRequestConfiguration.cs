using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class VendorRegistrationRequestConfiguration : IEntityTypeConfiguration<VendorRegistrationRequest>
    {
        public void Configure(EntityTypeBuilder<VendorRegistrationRequest> builder)
        {
            builder.Property(v => v.GovernmentIdType)
                .HasConversion<string>();

            builder.Property(v => v.GovernmentIdNumber)
                .HasMaxLength(50);

            builder.Property(v => v.GovernmentIdPhotoUrl)
                .HasMaxLength(1000);

            builder.Property(v => v.FirstName)
                .HasMaxLength(100);

            builder.Property(v => v.MiddleName)
                .HasMaxLength(100);

            builder.Property(v => v.LastName)
                .HasMaxLength(100);

            builder.Property(v => v.Email)
                .HasMaxLength(100);

            builder.HasIndex(v => v.Email)
                .IsUnique();

            builder.Property(v => v.Password)
                .HasMaxLength(1000);

            builder.Property(v => v.Status)
                .HasConversion<string>();

            builder.HasOne(u => u.ReviewedByUser)
                .WithMany(v => v.ReviewedVendorRegistrationRequests)
                .HasForeignKey(v => v.ReviewedBy)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(v => v.RequestedAt)
                .HasDefaultValueSql("timezone('utc', now())");

            builder.Property(v => v.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
        }
    }
}
