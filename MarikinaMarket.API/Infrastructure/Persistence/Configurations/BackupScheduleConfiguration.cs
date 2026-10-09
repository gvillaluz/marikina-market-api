using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class BackupScheduleConfiguration : IEntityTypeConfiguration<BackupSchedule>
    {
        public void Configure(EntityTypeBuilder<BackupSchedule> builder)
        {
            builder.Property(x => x.Id).ValueGeneratedNever();
            builder.Property(x => x.Frequency).HasConversion<string>().HasMaxLength(16);
            builder.Property(x => x.DayOfWeek).HasConversion<string>().HasMaxLength(16);
            builder.Property(x => x.Time).HasColumnType("time without time zone");
            builder.Property(x => x.NextRetryAt).HasColumnType("timestamp with time zone");
            builder.ToTable("backup_schedules", table =>
            {
                table.HasCheckConstraint("ck_backup_schedule_singleton", "id = 1");
                table.HasCheckConstraint("ck_backup_schedule_retention", "retention_days BETWEEN 1 AND 365");
                table.HasCheckConstraint("ck_backup_schedule_frequency",
                    "(frequency = 'Daily' AND day_of_week IS NULL) OR " +
                    "(frequency = 'Weekly' AND day_of_week IS NOT NULL AND day_of_week IN ('Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday'))");
            });
        }
    }
}
