using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.Property(x => x.Timestamp).HasDefaultValueSql("CURRENT_TIMESTAMP");
            builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
            builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Module).HasConversion<string>().HasMaxLength(32);
            builder.Property(x => x.TargetId).HasMaxLength(100);
            builder.Property(x => x.Result).HasConversion<string>().HasMaxLength(16);
            builder.Property(x => x.Details).HasMaxLength(500).IsRequired();
            // Actor IDs are historical values, not cascading relationships to current accounts.
            builder.HasIndex(x => new { x.Timestamp, x.Id });
            builder.HasIndex(x => new { x.UserId, x.Timestamp });
            builder.HasIndex(x => new { x.Module, x.Timestamp });
            builder.HasIndex(x => new { x.Result, x.Timestamp });
        }
    }
}
