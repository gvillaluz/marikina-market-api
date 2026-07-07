using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MarikinaMarket.API.Infrastructure.Persistence.Configurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasIndex(x => x.UserName)
                .IsUnique();

            builder.Property(x => x.FirstName)
                .HasMaxLength(100);

            builder.Property(x => x.MiddleName)
                .HasMaxLength(100);

            builder.Property(x => x.LastName)
                .HasMaxLength(100);

            builder.Property(x => x.Email)
                .HasMaxLength(255);

            builder.Property(x => x.CreatedAt)
                .HasDefaultValueSql("timezone('utc', now())");
        }
    }
}
