using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    public partial class UpdateOrdinancesAndPenalties : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep existing rows and IDs; update only catalog values that differ.
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_ordinances_ordinance_no;
                ALTER TABLE ordinances DROP COLUMN IF EXISTS description;
                CREATE INDEX IF NOT EXISTS ix_ordinances_ordinance_no ON ordinances (ordinance_no);

                UPDATE ordinances
                SET ordinance_no = regexp_replace(ordinance_no,
                    '^Ord[.]?[[:space:]]*(No[.]?|#)?[[:space:]]*([0-9]+).*$', '\2', 'i'),
                    updated_at = CURRENT_TIMESTAMP
                WHERE ordinance_no ~* '^Ord[.]?[[:space:]]*(No[.]?|#)?[[:space:]]*[0-9]+';

                INSERT INTO ordinances AS existing
                    (id, ordinance_no, series, market_code, title, category, created_at, updated_at, is_active)
                SELECT id, ordinance_no, series, market_code, title, category,
                    CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, true
                FROM (VALUES
                    (1, '145', '2006', 'SAMPLE-001', 'Peace & Order Code', 'Noise'),
                    (2, '104', '2007', 'SAMPLE-002', 'Market Identification Card', 'Licensing'),
                    (3, '123', '1993', 'SAMPLE-003', 'Posting of "Pahihintuloy sa Paghahanapbuhay"', 'Licensing'),
                    (4, '120', '2006', 'SAMPLE-004', 'Installation of "Timbangan Bayan"', 'WeightMeasures'),
                    (5, '160', '2001', 'SAMPLE-005', 'Weights and Measures', 'WeightMeasures'),
                    (6, '183', '1998', 'SAMPLE-006', 'Reconnection of Water/Electric Line', 'Licensing'),
                    (7, '011', '2014', 'SAMPLE-007', 'Prohibition to Buy Goods from Sidewalk Vendors', 'Obstruction'),
                    (8, '011', '2014', 'SAMPLE-008', 'Inadequate Attire in the Market', 'Sanitation'),
                    (9, '011', '2014', 'SAMPLE-009', 'Harboring or Accepting Illegal Vendors', 'Licensing'),
                    (10, '011', '2014', 'SAMPLE-010', 'Livestock and Poultry Products (Coloring/Water Injection)', 'Sanitation'),
                    (11, '04', '2011', 'SAMPLE-011', 'Mobile Vending Business/Activities', 'Traffic'),
                    (12, '011', '2014', 'SAMPLE-012', 'Use of Wooden Furniture in the Wet Section', 'Sanitation'),
                    (13, '011', '2014', 'SAMPLE-013', 'Tricycle Ban in the Market Zone', 'Traffic'),
                    (14, '117', '2007', 'SAMPLE-014', 'Regulation of Porters (Kargador)', 'Licensing'),
                    (15, '029', '2011', 'SAMPLE-015', 'Promotional Sampling and Leafleting', 'Obstruction'),
                    (16, '102', '2007', 'SAMPLE-016', 'Promotional Activities and Advertising', 'Licensing'),
                    (17, '149', '2007', 'SAMPLE-017', 'Streamer Regulation', 'Obstruction'),
                    (18, '147', '2007', 'SAMPLE-018', 'Shooting/Taping, Musical or Band Concerts, and Other Entertainment Activities', 'Noise'),
                    (19, '011', '2014', 'SAMPLE-019', 'General Violation by Public Market Stallholders (catch-all)', 'Licensing'),
                    (20, '011', '2014', 'SAMPLE-020', 'General Violation by Private Market Operators/Stallholders (catch-all)', 'Licensing'),
                    (21, '011', '2014', 'SAMPLE-021', 'Opposing, Resisting, or Interfering with a Market Officer/Enforcer', 'Obstruction')
                ) AS desired(id, ordinance_no, series, market_code, title, category)
                WHERE true
                ON CONFLICT (id) DO UPDATE SET
                    ordinance_no = EXCLUDED.ordinance_no,
                    series = EXCLUDED.series,
                    market_code = EXCLUDED.market_code,
                    title = EXCLUDED.title,
                    category = EXCLUDED.category,
                    updated_at = CURRENT_TIMESTAMP
                WHERE (existing.ordinance_no, existing.series, existing.market_code, existing.title, existing.category)
                    IS DISTINCT FROM
                    (EXCLUDED.ordinance_no, EXCLUDED.series, EXCLUDED.market_code, EXCLUDED.title, EXCLUDED.category);
                """);

            // Previous data migrations may have inserted explicit IDs without advancing sequences.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    catalog_table text;
                    identity_sequence text;
                    next_floor bigint;
                BEGIN
                    FOREACH catalog_table IN ARRAY ARRAY['ordinances', 'ordinance_penalty_tiers']
                    LOOP
                        identity_sequence := pg_get_serial_sequence(format('%I', catalog_table), 'id');
                        IF identity_sequence IS NOT NULL THEN
                            EXECUTE format('SELECT GREATEST(COALESCE(MAX(id), 1), (SELECT last_value FROM %s)) FROM %I',
                                identity_sequence, catalog_table) INTO next_floor;
                            PERFORM setval(identity_sequence::regclass, next_floor, true);
                        END IF;
                    END LOOP;
                END $$;
                """);

            // Retain existing tier IDs and additional offense tiers. Amounts remain provisional.
            migrationBuilder.Sql("""
                WITH desired AS (
                    SELECT ordinance.id AS ordinance_id, tier.offense AS offense_number,
                        tier.offense * 500 AS penalty_amount,
                        CASE tier.offense WHEN 1 THEN 'Minor' WHEN 2 THEN 'Moderate' ELSE 'High' END AS severity
                    FROM ordinances ordinance
                    CROSS JOIN generate_series(1, 3) AS tier(offense)
                    WHERE ordinance.id BETWEEN 1 AND 21
                )
                INSERT INTO ordinance_penalty_tiers AS existing
                    (ordinance_id, offense_number, penalty_amount, severity)
                SELECT desired.ordinance_id, desired.offense_number, desired.penalty_amount, desired.severity
                FROM desired
                WHERE NOT EXISTS (
                    SELECT 1 FROM ordinance_penalty_tiers current_tier
                    WHERE current_tier.ordinance_id = desired.ordinance_id
                        AND current_tier.offense_number = desired.offense_number
                        AND current_tier.penalty_amount = desired.penalty_amount
                        AND current_tier.severity = desired.severity
                )
                ON CONFLICT (ordinance_id, offense_number) DO UPDATE SET
                    penalty_amount = EXCLUDED.penalty_amount,
                    severity = EXCLUDED.severity
                WHERE (existing.penalty_amount, existing.severity)
                    IS DISTINCT FROM (EXCLUDED.penalty_amount, EXCLUDED.severity);
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException("Previous ordinance text and penalty amounts cannot be reconstructed. Restore a pre-migration backup to roll back.");
        }
    }
}
