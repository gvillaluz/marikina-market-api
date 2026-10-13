using MarikinaMarket.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarikinaMarket.API.Infrastructure.Persistence.Seeder
{
    public static class MarketDataSeeder
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            SeedMarketSections(modelBuilder);
        }

        private static void SeedMarketSections(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MarketSection>().HasData(
                new MarketSection
                {
                    Id = 1,
                    Name = "Fish and Seafood Section",
                    Description = "Designated area for vendors selling " +
                                  "fresh fish, shellfish, and other marine " +
                                  "products. Subject to strict sanitation " +
                                  "standards and daily cleaning requirements " +
                                  "per Chapter VIII of the Market Code of 2014.",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new MarketSection
                {
                    Id = 2,
                    Name = "Meat Section",
                    Description = "Designated area for vendors selling all " +
                                  "kinds of meat and meat products that passed " +
                                  "the inspection of the City Veterinary Office " +
                                  "in accordance with National Meat Inspection " +
                                  "Commission standards. Wooden furniture " +
                                  "prohibited per Section 58 of Market Code 2014.",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new MarketSection
                {
                    Id = 3,
                    Name = "Dry Goods Section",
                    Description = "Designated area for vendors selling " +
                                  "textiles, modiste and tailor supplies, " +
                                  "accessories, apparels, native products, " +
                                  "toiletries, novelties, toys, footwear, " +
                                  "kitchenwares, household articles, handbags, " +
                                  "and office supplies per Section 21(c) of " +
                                  "the Market Code of 2014.",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new MarketSection
                {
                    Id = 4,
                    Name = "Vegetable Section",
                    Description = "Designated area for vendors selling all " +
                                  "kinds of vegetables, fruits, coconuts, root " +
                                  "crops such as camote, cassava, gabi, and " +
                                  "other farm products per Section 21(f) of " +
                                  "the Market Code of 2014.",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new MarketSection
                {
                    Id = 5,
                    Name = "Groceries Section",
                    Description = "Designated area for vendors selling bakery " +
                                  "products, dairy, cold cuts, processed meat, " +
                                  "condiments, cigarettes, soap, charcoal, and " +
                                  "canned, bottled, boxed or sachet food products " +
                                  "per Section 21(e) of the Market Code of 2014.",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new MarketSection
                {
                    Id = 6,
                    Name = "Eatery Section",
                    Description = "Designated area for vendors selling all kinds " +
                                  "of cooked and prepared food. Food Safety and " +
                                  "Personal Hygiene Training required for all " +
                                  "eatery owners and helpers per Section 14 of " +
                                  "the Market Code of 2014. Highest sanitation " +
                                  "standards enforced under Chapter VIII.",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new MarketSection
                {
                    Id = 7,
                    Name = "Special Stalls",
                    Description = "Designated area for special commercial " +
                                  "establishments including restaurants, " +
                                  "pawnshops, hardware stores, drug stores, " +
                                  "beauty parlors, internet cafes, flower shops, " +
                                  "gift shops, magazine stands, and bayad centers " +
                                  "per Section 21(d) of the Market Code of 2014.",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new MarketSection
                {
                    Id = 8,
                    Name = "Miscellaneous Section",
                    Description = "Designated area for any other business " +
                                  "not classified under the established market " +
                                  "sections per Section 21(h) of the Market " +
                                  "Code of 2014. Subject to all general market " +
                                  "rules and sanitation standards.",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }

    }
}
