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
                .HasConversion<string>()
                .HasMaxLength(30);

            builder.Property(v => v.GovernmentIdNumber)
                .HasMaxLength(50);

            builder.Property(v => v.GovernmentIdPhotoUrl)
                .HasMaxLength(1000);

            builder.Property(v => v.BusinessDocumentPhotoUrl)
                .HasMaxLength(1000);

            builder.Property(v => v.BusinessName)
                .HasMaxLength(150);

            builder.Property(v => v.NatureOfBusiness)
                .HasMaxLength(150);

            builder.Property(v => v.FirstName)
                .HasMaxLength(100);

            builder.Property(v => v.MiddleName)
                .HasMaxLength(100);

            builder.Property(v => v.LastName)
                .HasMaxLength(100);

            builder.Property(v => v.HouseNumber)
                .HasMaxLength(50);

            builder.Property(v => v.Street)
                .HasMaxLength(100);

            builder.Property(v => v.Barangay)
                .HasMaxLength(100);

            builder.Property(v => v.City)
                .HasMaxLength(100);

            builder.Property(v => v.PhoneNumber)
                .HasMaxLength(20);

            builder.Property(v => v.Email)
                .HasMaxLength(255);

            builder.HasIndex(v => v.Email)
                .IsUnique();

            builder.Property(v => v.Password)
                .HasMaxLength(1000);

            builder.Property(v => v.StallNumber)
                .HasMaxLength(50);

            builder.Property(v => v.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            builder.HasOne(v => v.ReviewedByUser)
                .WithMany(u => u.ReviewedVendorRegistrationRequests)
                .HasForeignKey(v => v.ReviewedBy)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(v => v.RequestedAt)
                .HasDefaultValueSql("timezone('utc', now())");

            builder.Property(v => v.Version)
                .IsRowVersion()
                .HasColumnName("xmin")
                .HasColumnType("xid");
        }
    }
}
