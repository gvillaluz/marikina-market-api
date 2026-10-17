using System.Text.RegularExpressions;
using MarikinaMarket.API.Application.DTOs.Ordinance.Request;
using MarikinaMarket.API.Application.DTOs.Ordinance.Response;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace MarikinaMarket.Tests;

public class MarketSeedTests
{
    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
        .UseSnakeCaseNamingConvention().Options);

    [Fact]
    public void Model_DoesNotManageCatalogOrDemoRecordsAsSeeds()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        foreach (var type in new[] { typeof(Ordinance), typeof(OrdinancePenaltyTier), typeof(User),
            typeof(IdentityUserRole<int>), typeof(VendorProfile), typeof(VendorRegistrationRequest),
            typeof(Ticket), typeof(TicketViolation) })
            Assert.Empty(model.FindEntityType(type)!.GetSeedData());
    }

    [Fact]
    public void Migration_UpdatesOnlyCatalogAndSkipsMatchingValues()
    {
        using var context = CreateContext();
        Assert.False(context.Database.HasPendingModelChanges());
        var assembly = context.GetService<IMigrationsAssembly>();
        const string catalogMigration = "20261013080207_UpdateOrdinancesAndPenalties";
        var replacement = Assert.Single(assembly.Migrations, pair => pair.Key == catalogMigration);
        var operations = assembly.CreateMigration(replacement.Value, context.Database.ProviderName!).UpOperations;
        Assert.All(operations, operation => Assert.IsType<SqlOperation>(operation));
        var sql = context.GetService<IMigrator>().GenerateScript("20261012035730_AddedReportsEntity", catalogMigration);
        foreach (var table in new[] { "AspNetUsers", "AspNetUserRoles", "vendor_profiles",
            "vendor_registration_requests", "tickets", "ticket_violations", "community_service_logs", "shared." })
            Assert.DoesNotContain(table, sql);
        Assert.DoesNotContain("DELETE FROM", sql);
        Assert.Contains("DROP COLUMN IF EXISTS description", sql);
        Assert.Contains("IS DISTINCT FROM", sql);
        Assert.Contains("NOT EXISTS", sql);
        Assert.Contains("pg_get_serial_sequence", sql);
        Assert.Contains("START TRANSACTION", sql);
        Assert.Contains("COMMIT", sql);
        var entries = Regex.Matches(sql, @"^\s*\([0-9]+, '([0-9]+)', '([0-9]{4})', 'SAMPLE-[0-9]{3}',", RegexOptions.Multiline);
        Assert.Equal(21, entries.Count);
        Assert.Equal("145", entries[0].Groups[1].Value);
        Assert.Equal("011", entries[6].Groups[1].Value);
        Assert.Equal("04", entries[10].Groups[1].Value);
        Assert.Contains("offense * 500", sql);
        Assert.Contains("WHEN 1 THEN 'Minor' WHEN 2 THEN 'Moderate' ELSE 'High'", sql);
    }

    [Fact]
    public void OrdinanceSchema_AllowsRepeatedNumbersAndHasNoDescription()
    {
        using var context = CreateContext();
        var ordinance = context.Model.FindEntityType(typeof(Ordinance))!;
        Assert.Null(ordinance.FindProperty("Description"));
        Assert.DoesNotContain(ordinance.GetIndexes(), index => index.IsUnique
            && index.Properties.Select(p => p.Name).SequenceEqual(new[] { "OrdinanceNo" }));
        Assert.Null(typeof(SaveOrdinanceRequest).GetProperty("Description"));
        Assert.Null(typeof(OrdinanceSummaryResponse).GetProperty("Description"));
    }
}
