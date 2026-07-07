using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MarikinaMarket.API.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "shared");

            migrationBuilder.CreateSequence<int>(
                name: "UserSequence",
                schema: "shared");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    middle_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    must_changed_password = table.Column<bool>(type: "boolean", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "market_sections",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_market_sections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ordinances",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ordinance_no = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ordinances", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<int>(type: "integer", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_role_claims_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_asp_net_user_claims_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_asp_net_user_logins_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    role_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "AspNetRoles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_asp_net_user_roles_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asp_net_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_asp_net_user_tokens_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    token = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "vendor_registration_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    government_id_type = table.Column<string>(type: "text", nullable: false),
                    government_id_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    government_id_photo_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    business_document_photo_url = table.Column<string>(type: "text", nullable: false),
                    business_name = table.Column<string>(type: "text", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    middle_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reviewed_by = table.Column<int>(type: "integer", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendor_registration_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_vendor_registration_requests_asp_net_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "vendor_profiles",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    market_section_id = table.Column<int>(type: "integer", nullable: false),
                    business_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    stall_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    compliance_score = table.Column<int>(type: "integer", nullable: false),
                    score_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    qr_code_value = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    registered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vendor_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_vendor_profiles_market_sections_market_section_id",
                        column: x => x.market_section_id,
                        principalTable: "market_sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_vendor_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ordinance_penalty_tiers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ordinance_id = table.Column<int>(type: "integer", nullable: false),
                    offense_number = table.Column<int>(type: "integer", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    penalty_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ordinance_penalty_tiers", x => x.id);
                    table.ForeignKey(
                        name: "fk_ordinance_penalty_tiers_ordinances_ordinance_id",
                        column: x => x.ordinance_id,
                        principalTable: "ordinances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tickets",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    control_number = table.Column<string>(type: "text", nullable: false),
                    vendor_id = table.Column<int>(type: "integer", nullable: false),
                    market_section_id = table.Column<int>(type: "integer", nullable: false),
                    enforcer_id = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    total_payment_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    highest_severity = table.Column<string>(type: "text", nullable: false),
                    payment_status = table.Column<string>(type: "text", nullable: false),
                    penalty_type = table.Column<string>(type: "text", nullable: false),
                    community_service_hours = table.Column<int>(type: "integer", nullable: true),
                    receipt_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    primary_category = table.Column<string>(type: "text", nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tickets", x => x.id);
                    table.ForeignKey(
                        name: "fk_tickets_market_sections_market_section_id",
                        column: x => x.market_section_id,
                        principalTable: "market_sections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_users_enforcer_id",
                        column: x => x.enforcer_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tickets_vendor_profiles_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendor_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_evidences",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_id = table.Column<int>(type: "integer", nullable: false),
                    evidence_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    captured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_evidences", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_evidences_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ticket_violations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ticket_id = table.Column<int>(type: "integer", nullable: false),
                    ordinance_id = table.Column<int>(type: "integer", nullable: false),
                    offense_count = table.Column<int>(type: "integer", nullable: false),
                    penalty_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket_violations", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_violations_ordinances_ordinance_id",
                        column: x => x.ordinance_id,
                        principalTable: "ordinances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ticket_violations_tickets_ticket_id",
                        column: x => x.ticket_id,
                        principalTable: "tickets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "id", "concurrency_stamp", "name", "normalized_name" },
                values: new object[,]
                {
                    { 1, "a72b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d", "Admin", "ADMIN" },
                    { 2, "b83c4d5e-6f7a-8b9c-0d1e-2f3a4b5c6d7e", "Inspector", "INSPECTOR" },
                    { 3, "c94d5e6f-7a8b-9c0d-1e2f-3a4b5c6d7e8f", "Vendor", "VENDOR" }
                });

            migrationBuilder.InsertData(
                table: "market_sections",
                columns: new[] { "id", "created_at", "description", "name" },
                values: new object[,]
                {
                    { 1, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Designated area for vendors selling fresh fish, shellfish, and other marine products. Subject to strict sanitation standards and daily cleaning requirements per Chapter VIII of the Market Code of 2014.", "Fish and Seafood Section" },
                    { 2, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Designated area for vendors selling all kinds of meat and meat products that passed the inspection of the City Veterinary Office in accordance with National Meat Inspection Commission standards. Wooden furniture prohibited per Section 58 of Market Code 2014.", "Meat Section" },
                    { 3, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Designated area for vendors selling textiles, modiste and tailor supplies, accessories, apparels, native products, toiletries, novelties, toys, footwear, kitchenwares, household articles, handbags, and office supplies per Section 21(c) of the Market Code of 2014.", "Dry Goods Section" },
                    { 4, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Designated area for vendors selling all kinds of vegetables, fruits, coconuts, root crops such as camote, cassava, gabi, and other farm products per Section 21(f) of the Market Code of 2014.", "Vegetable Section" },
                    { 5, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Designated area for vendors selling bakery products, dairy, cold cuts, processed meat, condiments, cigarettes, soap, charcoal, and canned, bottled, boxed or sachet food products per Section 21(e) of the Market Code of 2014.", "Groceries Section" },
                    { 6, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Designated area for vendors selling all kinds of cooked and prepared food. Food Safety and Personal Hygiene Training required for all eatery owners and helpers per Section 14 of the Market Code of 2014. Highest sanitation standards enforced under Chapter VIII.", "Eatery Section" },
                    { 7, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Designated area for special commercial establishments including restaurants, pawnshops, hardware stores, drug stores, beauty parlors, internet cafes, flower shops, gift shops, magazine stands, and bayad centers per Section 21(d) of the Market Code of 2014.", "Special Stalls" },
                    { 8, new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Designated area for any other business not classified under the established market sections per Section 21(h) of the Market Code of 2014. Subject to all general market rules and sanitation standards.", "Miscellaneous Section" }
                });

            migrationBuilder.InsertData(
                table: "ordinances",
                columns: new[] { "id", "category", "created_at", "description", "ordinance_no", "title" },
                values: new object[,]
                {
                    { 1, "Obstruction", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sections 42, 47, and 53 of the Revised Marikina Market Code of 2014 (amending Ordinance No. 149, Series of 1999) prohibit peddling or hawking in passageways, placing items on corridors and walkways, and conducting any vending activities on streets and sidewalks within the Marikina Public Market Zone. Stall holders must strictly observe their designated stall boundaries at all times.", "Ord. No. 11, Series of 2014", "Revised Marikina Market Code of 2014 — Stall Boundary and Walkway Violations" },
                    { 2, "Noise", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Section 32 of the Revised Marikina Peace, Order, Public Safety and Security Code of 2006 regulates noise and revelries within the City. Audio-amplified equipment such as stereos, karaokes, videoke, and similar musical devices shall not play beyond normally accepted sound modulation after 10:00 PM. Market vendors are additionally prohibited from using sidewalks and streets as extensions of their stalls under Section 5, subject to fine of P1,000 and confiscation of goods. Alternative penalties including blood donation (for fines not exceeding P1,000) and community service are authorized under Appendix B.", "Ord. No. 145, Series of 2006", "Revised Marikina Peace, Order, Public Safety and Security Code of 2006" },
                    { 3, "Licensing", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ordinance No. 104, Series of 2007 amends Section 12 of the Marikina Market Code requiring all vendors and helpers in public and private markets within the City of Marikina to secure and display a valid Market Identification Card at all times. The Market ID costs Seventy-Five Pesos (P75.00) and must be renewed annually. Operating without a valid Market ID or business permit constitutes a violation subject to penalties under Section 79 of the Market Code of 2014.", "Ord. No. 104, Series of 2007", "Ordinance Amending Section 12 of the Marikina Market Code — Market Identification Card Requirements" },
                    { 4, "Sanitation", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Chapter VIII (Sections 66–75) of the Revised Marikina Market Code of 2014 governs the maintenance of market premises and sanitation standards. Stallholders must keep stalls clean at all times, use impervious materials on counters and walls per Section 72, protect cooked and raw foods from contamination per Section 70, clean stalls at the end of each business day per Section 73, and properly dispose of garbage per Section 69. All food eatery owners must complete Food Safety and Personal Hygiene Training per Section 14. A fine of One Thousand Pesos (P1,000.00) is imposed per violation. Third violation results in cancellation of license.", "Ord. No. 11, Series of 2014 — Chapter VIII", "Revised Marikina Market Code of 2014 — Sanitation and Cleanliness Standards" },
                    { 5, "WeightMeasures", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Chapter VI, Section 30 of the Revised Marikina Market Code of 2014 (amending Ordinance No. 160, Series of 2001 and Ordinance No. 283, Series of 1997) prohibits the use of underweight scales, placement of concealed materials in weighing scales, and any manipulation that reflects a weight other than the true weight of goods. All weighing devices must be registered with the City Treasury and submitted for annual re-inspection. Confiscated scales must be redeemed within five (5) working days. A specific graduated penalty schedule applies independently from the general market code penalties.", "Ord. No. 11, Series of 2014 — Chapter VI", "Revised Marikina Market Code of 2014 — Weights and Measures Compliance" }
                });

            migrationBuilder.InsertData(
                table: "ordinance_penalty_tiers",
                columns: new[] { "id", "offense_number", "ordinance_id", "penalty_amount", "severity" },
                values: new object[,]
                {
                    { 1, 1, 1, 1000.00m, "Low" },
                    { 2, 2, 1, 1500.00m, "Medium" },
                    { 3, 3, 1, 2000.00m, "High" },
                    { 4, 4, 1, 5000.00m, "High" },
                    { 5, 1, 2, 500.00m, "Low" },
                    { 6, 2, 2, 1000.00m, "Medium" },
                    { 7, 3, 2, 2000.00m, "High" },
                    { 8, 4, 2, 3000.00m, "High" },
                    { 9, 1, 3, 100.00m, "Low" },
                    { 10, 2, 3, 500.00m, "Medium" },
                    { 11, 3, 3, 1000.00m, "High" },
                    { 12, 4, 3, 2000.00m, "High" },
                    { 13, 1, 4, 1000.00m, "Medium" },
                    { 14, 2, 4, 1500.00m, "High" },
                    { 15, 3, 4, 2000.00m, "High" },
                    { 16, 1, 5, 1500.00m, "Medium" },
                    { 17, 2, 5, 3000.00m, "High" },
                    { 18, 3, 5, 5000.00m, "High" },
                    { 19, 4, 5, 5000.00m, "High" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_role_claims_role_id",
                table: "AspNetRoleClaims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_claims_user_id",
                table: "AspNetUserClaims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_logins_user_id",
                table: "AspNetUserLogins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_user_roles_role_id",
                table: "AspNetUserRoles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_user_name",
                table: "AspNetUsers",
                column: "user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_market_sections_name",
                table: "market_sections",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ordinance_penalty_tiers_ordinance_id_offense_number",
                table: "ordinance_penalty_tiers",
                columns: new[] { "ordinance_id", "offense_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ordinances_ordinance_no",
                table: "ordinances",
                column: "ordinance_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_evidences_ticket_id",
                table: "ticket_evidences",
                column: "ticket_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_violations_ordinance_id",
                table: "ticket_violations",
                column: "ordinance_id");

            migrationBuilder.CreateIndex(
                name: "ix_ticket_violations_ticket_id_ordinance_id",
                table: "ticket_violations",
                columns: new[] { "ticket_id", "ordinance_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_control_number",
                table: "tickets",
                column: "control_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tickets_enforcer_id",
                table: "tickets",
                column: "enforcer_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_market_section_id",
                table: "tickets",
                column: "market_section_id");

            migrationBuilder.CreateIndex(
                name: "ix_tickets_vendor_id_primary_category_issued_at",
                table: "tickets",
                columns: new[] { "vendor_id", "primary_category", "issued_at" });

            migrationBuilder.CreateIndex(
                name: "ix_vendor_profiles_market_section_id_stall_number",
                table: "vendor_profiles",
                columns: new[] { "market_section_id", "stall_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vendor_profiles_qr_code_value",
                table: "vendor_profiles",
                column: "qr_code_value",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vendor_profiles_user_id",
                table: "vendor_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vendor_registration_requests_email",
                table: "vendor_registration_requests",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_vendor_registration_requests_reviewed_by",
                table: "vendor_registration_requests",
                column: "reviewed_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "ordinance_penalty_tiers");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "ticket_evidences");

            migrationBuilder.DropTable(
                name: "ticket_violations");

            migrationBuilder.DropTable(
                name: "vendor_registration_requests");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "ordinances");

            migrationBuilder.DropTable(
                name: "tickets");

            migrationBuilder.DropTable(
                name: "vendor_profiles");

            migrationBuilder.DropTable(
                name: "market_sections");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropSequence(
                name: "UserSequence",
                schema: "shared");
        }
    }
}
