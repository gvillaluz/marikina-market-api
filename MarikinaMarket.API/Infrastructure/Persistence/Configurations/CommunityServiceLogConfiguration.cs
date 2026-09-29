using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class CommunityServiceLogConfiguration
        : IEntityTypeConfiguration<CommunityServiceLog>
    {
        public void Configure(EntityTypeBuilder<CommunityServiceLog> builder)
        {
            builder.HasOne(x => x.Ticket)
                .WithMany(x => x.CommunityServiceLogs)
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.RecordedBy)
                .WithMany()
                .HasForeignKey(x => x.RecordedById)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Property(x => x.HoursWorked)
                .HasPrecision(5, 2);

            builder.Property(x => x.ProofUrl)
                .HasMaxLength(500);

            builder.Property(x => x.CreatedAt)
                .HasDefaultValueSql("timezone('utc', now())");
        }
    }
}