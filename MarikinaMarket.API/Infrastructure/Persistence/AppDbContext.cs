using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Infrastructure.Persistence.Seeder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MarikinaMarket.API.Infrastructure.Persistence
{
    public class AppDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<MarketSection> MarketSections { get; set; }
        public DbSet<Ordinance> Ordinances { get; set; }
        public DbSet<OrdinancePenaltyTier> OrdinancePenaltyTiers { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<TicketEvidence> TicketEvidences { get; set; }
        public DbSet<TicketViolation> TicketViolations { get; set; }
        public DbSet<VendorProfile> VendorProfiles { get; set; }
        public DbSet<VendorRegistrationRequest> VendorRegistrationRequests { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<UserDeviceToken> UserDeviceTokens { get; set; }
        public DbSet<Notification> Notifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.HasSequence<int>("UserSequence", schema: "shared")
                .StartsAt(1)
                .IncrementsBy(1);

            modelBuilder.Entity<IdentityRole<int>>().HasData(
                new IdentityRole<int>
                {
                    Id = 1,
                    Name = "Admin",
                    NormalizedName = "ADMIN",
                    ConcurrencyStamp = "a72b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d"
                },
                new IdentityRole<int>
                {
                    Id = 2,
                    Name = "Enforcer",
                    NormalizedName = "ENFORCER",
                    ConcurrencyStamp = "b83c4d5e-6f7a-8b9c-0d1e-2f3a4b5c6d7e"
                },
                new IdentityRole<int>
                {
                    Id = 3,
                    Name = "Vendor",
                    NormalizedName = "VENDOR",
                    ConcurrencyStamp = "c94d5e6f-7a8b-9c0d-1e2f-3a4b5c6d7e8f"
                }
            );

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            MarketDataSeeder.Seed(modelBuilder);
        }
    }
}
