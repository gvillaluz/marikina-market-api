using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.HasOne(notification => notification.Vendor)
                .WithMany()
                .HasForeignKey(notification => notification.VendorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(notification => notification.VendorIsRead).HasDefaultValue(false);
            builder.HasIndex(notification => new { notification.VendorId, notification.CreatedAt, notification.Id })
                .IsDescending(false, true, true);
        }
    }
}
