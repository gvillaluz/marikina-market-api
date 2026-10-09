using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class BackupConfiguration : IEntityTypeConfiguration<Backup>
    {
        public void Configure(EntityTypeBuilder<Backup> builder)
        {
            builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(16);
            builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
            builder.Property(x => x.B2Key).HasMaxLength(250);
            builder.Property(x => x.ErrorMessage).HasMaxLength(500);
            builder.HasIndex(x => x.DateTime);
            builder.HasIndex(x => new { x.IsFinalized, x.Status, x.ExpiresAt });
            builder.HasIndex(x => x.B2Key).IsUnique();
        }
    }
}
