using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class UserDeviceTokenConfiguration : IEntityTypeConfiguration<UserDeviceToken>
    {
        public void Configure(EntityTypeBuilder<UserDeviceToken> builder)
        {
            builder.HasKey(t => t.Id);

            builder.Property(t => t.DeviceToken)
                .IsRequired()
                .HasMaxLength(500);

            builder.HasIndex(t => t.DeviceToken)
                .IsUnique();

            builder.HasIndex(t => t.UserId);

            builder.HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(t => t.CreatedAt)
                .HasDefaultValueSql("now()");

            builder.Property(t => t.LastUsedAt)
                .HasDefaultValueSql("now()");
        }
    }
}