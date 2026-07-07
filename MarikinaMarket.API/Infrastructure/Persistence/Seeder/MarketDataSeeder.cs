using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarikinaMarket.API.Infrastructure.Persistence.Seeder
{
    public static class MarketDataSeeder
    {
        public static void Seed(ModelBuilder modelBuilder)
        {
            SeedMarketSections(modelBuilder);
            SeedOrdinances(modelBuilder);
            SeedOrdinancePenaltyTiers(modelBuilder);
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

        private static void SeedOrdinances(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Ordinance>().HasData(

                // ── OBSTRUCTION ─────────────────────────────────────────
                // Original: Ord. No. 149, Series of 1999 (Market Code)
                // As revised by: Ord. No. 11, Series of 2014
                // Paper ticket reference: "Ord. #149 series of ???? — Market Code"
                // Sections 42, 47, 53: prohibits use of passageways, aisles,
                // corridors, walkways, and streets for display/vending
                // Section 79: 1st=P1,000 | 2nd=P1,500 | 3rd=Closure
                new Ordinance
                {
                    Id = 1,
                    OrdinanceNo = "Ord. No. 11, Series of 2014",
                    Title = "Revised Marikina Market Code of 2014 " +
                                  "— Stall Boundary and Walkway Violations",
                    Description = "Sections 42, 47, and 53 of the Revised " +
                                  "Marikina Market Code of 2014 (amending " +
                                  "Ordinance No. 149, Series of 1999) prohibit " +
                                  "peddling or hawking in passageways, placing " +
                                  "items on corridors and walkways, and conducting " +
                                  "any vending activities on streets and sidewalks " +
                                  "within the Marikina Public Market Zone. Stall " +
                                  "holders must strictly observe their designated " +
                                  "stall boundaries at all times.",
                    Category = ViolationCategory.Obstruction,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },

                // ── NOISE ────────────────────────────────────────────────
                // Ord. No. 145, Series of 2006 (Peace, Order, Public Safety
                // and Security Code)
                // Section 32: regulates noise and revelries
                // Audio-amplified machines not allowed beyond 10:00 PM
                // Appendix A penalties for market stall holders using
                // sidewalks: P1,000 + confiscation
                // Paper ticket reference: "Ord. #145 series of 2006 — Peace & Order Code"
                new Ordinance
                {
                    Id = 2,
                    OrdinanceNo = "Ord. No. 145, Series of 2006",
                    Title = "Revised Marikina Peace, Order, Public " +
                                  "Safety and Security Code of 2006",
                    Description = "Section 32 of the Revised Marikina Peace, " +
                                  "Order, Public Safety and Security Code of 2006 " +
                                  "regulates noise and revelries within the City. " +
                                  "Audio-amplified equipment such as stereos, " +
                                  "karaokes, videoke, and similar musical devices " +
                                  "shall not play beyond normally accepted sound " +
                                  "modulation after 10:00 PM. Market vendors are " +
                                  "additionally prohibited from using sidewalks and " +
                                  "streets as extensions of their stalls under " +
                                  "Section 5, subject to fine of P1,000 and " +
                                  "confiscation of goods. Alternative penalties " +
                                  "including blood donation (for fines not " +
                                  "exceeding P1,000) and community service are " +
                                  "authorized under Appendix B.",
                    Category = ViolationCategory.Noise,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },

                // ── LICENSING ────────────────────────────────────────────
                // Ord. No. 104, Series of 2007 amends Section 12 of the
                // Market Code regarding Market Identification Card
                // Market Code Section 12: requires Market ID for all
                // vendors and helpers
                // Penalty for Section 12 violation: P100.00
                // Paper ticket reference: "Ord. #104 series of 2007 — Market I.D."
                new Ordinance
                {
                    Id = 3,
                    OrdinanceNo = "Ord. No. 104, Series of 2007",
                    Title = "Ordinance Amending Section 12 of the " +
                                  "Marikina Market Code — Market Identification " +
                                  "Card Requirements",
                    Description = "Ordinance No. 104, Series of 2007 amends " +
                                  "Section 12 of the Marikina Market Code " +
                                  "requiring all vendors and helpers in public " +
                                  "and private markets within the City of Marikina " +
                                  "to secure and display a valid Market " +
                                  "Identification Card at all times. The Market ID " +
                                  "costs Seventy-Five Pesos (P75.00) and must be " +
                                  "renewed annually. Operating without a valid " +
                                  "Market ID or business permit constitutes a " +
                                  "violation subject to penalties under Section 79 " +
                                  "of the Market Code of 2014.",
                    Category = ViolationCategory.Licensing,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },

                // ── SANITATION ───────────────────────────────────────────
                // Ord. No. 11, Series of 2014 — Chapter VIII
                // Sections 66-75: market sanitation and cleanliness
                // Section 79: fine of P1,000 per Chapter VIII violation
                // 3rd violation = cancellation of license
                new Ordinance
                {
                    Id = 4,
                    OrdinanceNo = "Ord. No. 11, Series of 2014 — Chapter VIII",
                    Title = "Revised Marikina Market Code of 2014 " +
                                  "— Sanitation and Cleanliness Standards",
                    Description = "Chapter VIII (Sections 66–75) of the Revised " +
                                  "Marikina Market Code of 2014 governs the " +
                                  "maintenance of market premises and sanitation " +
                                  "standards. Stallholders must keep stalls clean " +
                                  "at all times, use impervious materials on " +
                                  "counters and walls per Section 72, protect " +
                                  "cooked and raw foods from contamination per " +
                                  "Section 70, clean stalls at the end of each " +
                                  "business day per Section 73, and properly " +
                                  "dispose of garbage per Section 69. All food " +
                                  "eatery owners must complete Food Safety and " +
                                  "Personal Hygiene Training per Section 14. " +
                                  "A fine of One Thousand Pesos (P1,000.00) is " +
                                  "imposed per violation. Third violation results " +
                                  "in cancellation of license.",
                    Category = ViolationCategory.Sanitation,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                },

                // ── WEIGHT & MEASURES ────────────────────────────────────
                // Ord. No. 11, Series of 2014 — Chapter VI, Section 30
                // (originally codified under Ord. No. 283, Series of 1997,
                // amended by Ord. No. 160, Series of 2001)
                // Specific penalty schedule: P1,500 | P3,000 | P5,000+revocation
                new Ordinance
                {
                    Id = 5,
                    OrdinanceNo = "Ord. No. 11, Series of 2014 — Chapter VI",
                    Title = "Revised Marikina Market Code of 2014 " +
                                  "— Weights and Measures Compliance",
                    Description = "Chapter VI, Section 30 of the Revised " +
                                  "Marikina Market Code of 2014 (amending " +
                                  "Ordinance No. 160, Series of 2001 and " +
                                  "Ordinance No. 283, Series of 1997) prohibits " +
                                  "the use of underweight scales, placement of " +
                                  "concealed materials in weighing scales, and " +
                                  "any manipulation that reflects a weight other " +
                                  "than the true weight of goods. All weighing " +
                                  "devices must be registered with the City " +
                                  "Treasury and submitted for annual re-inspection. " +
                                  "Confiscated scales must be redeemed within five " +
                                  "(5) working days. A specific graduated penalty " +
                                  "schedule applies independently from the general " +
                                  "market code penalties.",
                    Category = ViolationCategory.WeightMeasures,
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }
            );
        }

        private static void SeedOrdinancePenaltyTiers(ModelBuilder modelBuilder)
        {
            var tiers = new List<OrdinancePenaltyTier>();
            int id = 1;

            // ── Ordinance 1: Market Code — Obstruction ───────────────────
            // Source: Ord. No. 11, Series of 2014, Section 79
            // "For Private Market Operators/Stallholders:
            //  First Offense  — P1,000
            //  Second Offense — P1,500
            //  Third Offense  — Closure (P2,000 as fine equivalent)
            //  Fourth+        — Revocation (P5,000 max per LGU Code)"
            // Alternative penalty: Blood Donation allowed for ≤ P1,000 (1st offense only)
            // Community Service: 4 hrs per P100–P500 | 8 hrs per P1,000+
            tiers.AddRange(new[]
            {
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 1,
                OffenseNumber = 1, Severity = Severity.Low,
                PenaltyAmount = 1000.00m },
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 1,
                OffenseNumber = 2, Severity = Severity.Medium,
                PenaltyAmount = 1500.00m },
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 1,
                OffenseNumber = 3, Severity = Severity.High,
                PenaltyAmount = 2000.00m },
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 1,
                OffenseNumber = 4, Severity = Severity.High,
                PenaltyAmount = 5000.00m },
        });

            // ── Ordinance 2: Peace & Order Code — Noise ──────────────────
            // Source: Ord. No. 145, Series of 2006
            // Section 5 (market stall sidewalk use): P1,000 + confiscation
            // Section 32 (noise/revelries): no specific fine stated;
            // applies general schedule aligned with community service table:
            //   P500 fine = 3 hrs community service (Appendix B)
            //   P1,000 fine = 4 hrs community service
            // 3rd offense for market stall holders = revocation
            // Blood Donation: allowed for fines ≤ P1,000 (Appendix B, Sec. 47)
            tiers.AddRange(new[]
            {
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 2,
                OffenseNumber = 1, Severity = Severity.Low,
                PenaltyAmount = 500.00m },
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 2,
                OffenseNumber = 2, Severity = Severity.Medium,
                PenaltyAmount = 1000.00m },
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 2,
                OffenseNumber = 3, Severity = Severity.High,
                PenaltyAmount = 2000.00m },
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 2,
                OffenseNumber = 4, Severity = Severity.High,
                PenaltyAmount = 3000.00m },
        });

            // ── Ordinance 3: Market ID — Licensing ───────────────────────
            // Source: Ord. No. 104, Series of 2007 (amends Market Code Sec. 12)
            // Market Code Section 79: "For violation of Chapter III,
            // Section 12 — a fine of ONE HUNDRED PESOS (P100.00)"
            // This is the base fine; escalation follows general schedule
            // Operating entirely without permit = general schedule P1,000+
            // Using combination of actual P100 base with graduated scale
            tiers.AddRange(new[]
            {
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 3,
                OffenseNumber = 1, Severity = Severity.Low,
                PenaltyAmount = 100.00m },
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 3,
                OffenseNumber = 2, Severity = Severity.Medium,
                PenaltyAmount = 500.00m },
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 3,
                OffenseNumber = 3, Severity = Severity.High,
                PenaltyAmount = 1000.00m },
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 3,
                OffenseNumber = 4, Severity = Severity.High,
                PenaltyAmount = 2000.00m },
        });

            // ── Ordinance 4: Market Code Chapter VIII — Sanitation ───────
            // Source: Ord. No. 11, Series of 2014, Section 79 Chapter VIII
            // "For violation of Chapter VIII — a fine of ONE THOUSAND PESOS
            //  (P1,000.00). A third violation shall subject the offender to
            //  cancellation of license in addition to penalty."
            // Blood Donation: NOT available (fine exceeds P1,000 for 2nd+)
            // Community Service: 8 hours per P1,000+ fine
            tiers.AddRange(new[]
            {
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 4,
                OffenseNumber = 1, Severity = Severity.Medium,
                PenaltyAmount = 1000.00m },
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 4,
                OffenseNumber = 2, Severity = Severity.High,
                PenaltyAmount = 1500.00m },
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 4,
                OffenseNumber = 3, Severity = Severity.High,
                PenaltyAmount = 2000.00m }
        });

            // ── Ordinance 5: Market Code Chapter VI — Weights & Measures ──
            // Source: Ord. No. 11, Series of 2014, Section 79 (Chapter VI)
            // EXACT AMOUNTS FROM ACTUAL ORDINANCE:
            // "First Offense  — P1,500.00
            //  Second Offense — P3,000.00 within Five (5) days
            //  Third Offense  — P5,000.00 + revocation and cancellation
            //                   of permits and closure of establishment"
            // Note: ordinance text reads "One Thousand Pesos (P1,500.00)"
            // which appears to be a typographical error in the original;
            // P1,500 is used as the stated amount matches P1,500 not P1,000
            tiers.AddRange(new[]
            {
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 5,
                OffenseNumber = 1, Severity = Severity.Medium,
                PenaltyAmount = 1500.00m },
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 5,
                OffenseNumber = 2, Severity = Severity.High,
                PenaltyAmount = 3000.00m },
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 5,
                OffenseNumber = 3, Severity = Severity.High,
                PenaltyAmount = 5000.00m },
            new OrdinancePenaltyTier { Id = id++, OrdinanceId = 5,
                OffenseNumber = 4, Severity = Severity.High,
                PenaltyAmount = 5000.00m },
        });

            modelBuilder.Entity<OrdinancePenaltyTier>().HasData(tiers);
        }
    }
}
