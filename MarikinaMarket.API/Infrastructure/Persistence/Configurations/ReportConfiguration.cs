using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class ReportConfiguration : IEntityTypeConfiguration<Report>
    {
        public void Configure(EntityTypeBuilder<Report> builder)
        {
            builder.Property(x => x.Type).HasMaxLength(16);
            builder.Property(x => x.GeneratedByName).HasMaxLength(256);
            builder.Property(x => x.ResolvedTicketPenalties).HasPrecision(18, 2);
            builder.Property(x => x.PendingTicketPenalties).HasPrecision(18, 2);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.GeneratedById).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.DateGenerated, x.Id });
        }
    }
}
