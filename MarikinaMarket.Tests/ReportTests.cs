using System.Net;
using System.Reflection;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Application;
using MarikinaMarket.API.Application.DTOs.Reports.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MarikinaMarket.Tests;

public class ReportTests
{
    [Fact]
    public async Task PreviewCombinesMetricsAndUsesInclusiveManilaDate()
    {
        var fixture = new ReportFixture();
        var result = await fixture.Service.PreviewAsync("2026-10-12", "Warning & Ticket");
        Assert.Equal(new DateTime(2026, 10, 11, 16, 0, 0, DateTimeKind.Utc), fixture.Tickets.Start);
        Assert.Equal(new DateTime(2026, 10, 12, 16, 0, 0, DateTimeKind.Utc), fixture.Tickets.End);
        Assert.Null(fixture.Tickets.Type);
        Assert.Equal(7, result.Summary.Total.TotalRecords);
        Assert.Equal(1200m, result.Summary.Total.AssessedPenalties);
        Assert.Equal(5, result.Summary.Resolved.TotalRecords);
        Assert.Equal(700m, result.Summary.Resolved.AssessedPenalties);
        Assert.Equal(2, result.Summary.Pending.TotalRecords);
        Assert.Equal(500m, result.Summary.Pending.AssessedPenalties);
        Assert.Equal(3, result.Summary.WarningsIssued!.Resolved.TotalRecords);
        Assert.Equal(0m, result.Summary.WarningsIssued.Total.AssessedPenalties);
        Assert.Equal(4, result.Summary.TicketsIssued!.Total.TotalRecords);
        Assert.Empty(fixture.Reports.Items);
        Assert.Equal(0, fixture.Unit.Begins);
    }

    [Theory]
    [InlineData("Warning", 3, 0)]
    [InlineData("Ticket", 4, 1200)]
    public async Task PreviewFiltersType(string type, int count, decimal penalties)
    {
        var fixture = new ReportFixture();
        var result = await fixture.Service.PreviewAsync("2026-10-12", type);
        Assert.Equal(count, result.Summary.Total.TotalRecords);
        Assert.Equal(penalties, result.Summary.Total.AssessedPenalties);
        Assert.Equal(type == "Warning" ? ViolationType.Warning : ViolationType.Ticket, fixture.Tickets.Type);
        Assert.Equal(type == "Warning", result.Summary.WarningsIssued is not null);
        Assert.Equal(type == "Ticket", result.Summary.TicketsIssued is not null);
    }

    [Fact]
    public async Task GenerationSavesSnapshotAndDetailDoesNotRequeryTickets()
    {
        var fixture = new ReportFixture();
        var result = await fixture.Service.GenerateAsync(7, new GenerateReportRequest
            { From = "2026-10-01", To = "2026-10-12", Type = "Warning & Ticket" });
        Assert.Equal(7, result.GeneratedBy.Id);
        Assert.Equal("Juan Dela Cruz", result.GeneratedBy.Name);
        Assert.Equal(DateTimeKind.Utc, result.DateGenerated.Kind);
        Assert.Equal(1, fixture.Unit.Begins);
        Assert.Equal(1, fixture.Unit.Saves);
        Assert.Equal(1, fixture.Unit.Commits);
        fixture.Tickets.Rows.Clear();
        var detail = await fixture.Service.GetDetailAsync(result.Id);
        Assert.Equal(7, detail.Summary.Total.TotalRecords);
        Assert.Equal(1200m, detail.Summary.Total.AssessedPenalties);
        Assert.Equal(1, fixture.Tickets.Calls);
    }

    [Fact]
    public async Task GenerationRollsBackWhenSaveFails()
    {
        var fixture = new ReportFixture();
        fixture.Unit.FailSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.GenerateAsync(7,
            new GenerateReportRequest { From = "2026-10-01", To = "2026-10-12", Type = "Ticket" }));
        Assert.Equal(1, fixture.Unit.Rollbacks);
        Assert.Equal(0, fixture.Unit.Commits);
    }

    [Theory]
    [InlineData(null, "2026-10-12", "Ticket")]
    [InlineData("2026-10-13", "2026-10-12", "Ticket")]
    [InlineData("2026-02-30", "2026-10-12", "Ticket")]
    [InlineData("2026-10-01", "9999-12-31", "Ticket")]
    [InlineData("2026-10-01", "2026-10-12", "0")]
    [InlineData("2026-10-01", "2026-10-12", null)]
    public async Task RejectsInvalidGenerationBeforeWriting(string? from, string? to, string? type)
    {
        var fixture = new ReportFixture();
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.GenerateAsync(7,
            new GenerateReportRequest { From = from, To = to, Type = type }));
        Assert.Equal(0, fixture.Unit.Begins);
        Assert.Equal(0, fixture.Tickets.Calls);
    }

    [Fact]
    public async Task EmptyPreviewAndMissingDetailHaveExpectedBehavior()
    {
        var fixture = new ReportFixture();
        fixture.Tickets.Rows.Clear();
        var result = await fixture.Service.PreviewAsync("2026-10-12", "Warning & Ticket");
        Assert.Equal(0, result.Summary.Total.TotalRecords);
        Assert.Equal(0m, result.Summary.Total.AssessedPenalties);
        await Assert.ThrowsAsync<RecordNotFoundException>(() => fixture.Service.GetDetailAsync(1));
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.GetDetailAsync(0));
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Service.GetHistoryAsync(-1));
    }

    [Fact]
    public async Task HistoryReturnsTenRowsTotalAndHasMore()
    {
        var fixture = new ReportFixture();
        for (var id = 1; id <= 12; id++)
            fixture.Reports.Items.Add(new Report { Id = id, Type = "Ticket", GeneratedByName = "Juan Dela Cruz" });
        var page = await fixture.Service.GetHistoryAsync(0);
        Assert.Equal(12, page.Total);
        Assert.True(page.HasMore);
        Assert.Equal(10, page.Items.Count);
        Assert.Equal(12, page.Items[0].Id);
        var last = await fixture.Service.GetHistoryAsync(10);
        Assert.False(last.HasMore);
        Assert.Equal(2, last.Items.Count);
    }

    [Theory]
    [InlineData("HeadAdmin")]
    [InlineData("AdminOfficer")]
    public async Task HttpGenerationHistoryAndDetailReturnSnapshotJson(string role)
    {
        var fixture = new ReportFixture();
        await using var host = await AuditHost.CreateAsync(services =>
            services.AddSingleton<IReportService>(fixture.Service));
        var response = await host.SendAsync(HttpMethod.Post, "/api/admin/reports", role,
            """{"from":"2026-10-01","to":"2026-10-12","type":"Warning & Ticket","generated_by_id":999}""");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal(7, root.GetProperty("generated_by").GetProperty("id").GetInt32());
        Assert.Equal("2026-10-01", root.GetProperty("date_range").GetProperty("from").GetString());
        Assert.Equal(7, root.GetProperty("summary").GetProperty("total").GetProperty("total_records").GetInt32());
        Assert.Equal(1200m, root.GetProperty("summary").GetProperty("total").GetProperty("assessed_penalties").GetDecimal());
        var id = root.GetProperty("id").GetInt32();
        fixture.Tickets.Rows.Clear();
        var detail = await host.SendAsync(HttpMethod.Get, $"/api/admin/reports/{id}", role);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(await response.Content.ReadAsStringAsync(), await detail.Content.ReadAsStringAsync());
        var history = await host.SendAsync(HttpMethod.Get, "/api/admin/reports", role);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        using var historyJson = JsonDocument.Parse(await history.Content.ReadAsStringAsync());
        Assert.Equal(1, historyJson.RootElement.GetProperty("total").GetInt32());
        Assert.False(historyJson.RootElement.GetProperty("has_more").GetBoolean());
        Assert.False(historyJson.RootElement.GetProperty("items")[0].TryGetProperty("summary", out _));
    }

    [Fact]
    public async Task HttpPreviewDefaultsToBothAndDoesNotSave()
    {
        var fixture = new ReportFixture();
        await using var host = await AuditHost.CreateAsync(services =>
            services.AddSingleton<IReportService>(fixture.Service));
        var response = await host.SendAsync(HttpMethod.Get, "/api/admin/reports/preview?date=2026-10-12");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Warning & Ticket", json.RootElement.GetProperty("type").GetString());
        Assert.Equal(7, json.RootElement.GetProperty("summary").GetProperty("total").GetProperty("total_records").GetInt32());
        Assert.Empty(fixture.Reports.Items);
    }

    [Theory]
    [InlineData("/api/admin/reports/preview", 400)]
    [InlineData("/api/admin/reports/preview?date=bad", 400)]
    [InlineData("/api/admin/reports/preview?date=2026-10-12&type=Both", 400)]
    [InlineData("/api/admin/reports?offset=-1", 400)]
    [InlineData("/api/admin/reports?offset=bad", 400)]
    [InlineData("/api/admin/reports/0", 400)]
    [InlineData("/api/admin/reports/99", 404)]
    public async Task HttpRejectsInvalidQueries(string route, int status)
    {
        var fixture = new ReportFixture();
        await using var host = await AuditHost.CreateAsync(services =>
            services.AddSingleton<IReportService>(fixture.Service));
        var response = await host.SendAsync(HttpMethod.Get, route);
        Assert.Equal(status, (int)response.StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"from\":\"2026-10-13\",\"to\":\"2026-10-12\",\"type\":\"Ticket\"}")]
    [InlineData("{\"from\":\"2026-10-01\",\"to\":\"2026-10-12\",\"type\":0}")]
    public async Task HttpRejectsInvalidBodies(string body)
    {
        var fixture = new ReportFixture();
        await using var host = await AuditHost.CreateAsync(services =>
            services.AddSingleton<IReportService>(fixture.Service));
        var response = await host.SendAsync(HttpMethod.Post, "/api/admin/reports", body: body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(fixture.Reports.Items);
    }
    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("MarketVendor", HttpStatusCode.Forbidden)]
    [InlineData("MarketEnforcer", HttpStatusCode.Forbidden)]
    public async Task ReportsRequireAdministrator(string? role, HttpStatusCode expected)
    {
        await using var host = await AuditHost.CreateAsync();
        var response = await host.SendAsync(HttpMethod.Get, "/api/admin/reports", role);
        Assert.Equal(expected, response.StatusCode);
    }
}

internal sealed class ReportFixture
{
    public MemoryReportRepository Reports { get; } = new();
    public ReportUnitOfWork Unit { get; } = new();
    public ReportTicketRepository Tickets { get; }
    public ReportService Service { get; }
    public ReportFixture()
    {
        var tickets = DispatchProxy.Create<ITicketRepository, ReportTicketRepository>();
        Tickets = (ReportTicketRepository)(object)tickets;
        Service = new ReportService(Reports, tickets, AuditUserRepositoryStub.Create(), Unit);
    }
}

public class ReportTicketRepository : DispatchProxy
{
    public DateTime Start { get; private set; }
    public DateTime End { get; private set; }
    public ViolationType? Type { get; private set; }
    public int Calls { get; private set; }
    public List<TicketReportSummary> Rows { get; } =
    [
        new() { Type = ViolationType.Warning, IsResolved = true, TotalRecords = 3 },
        new() { Type = ViolationType.Ticket, IsResolved = true, TotalRecords = 2, AssessedPenalties = 700m },
        new() { Type = ViolationType.Ticket, IsResolved = false, TotalRecords = 2, AssessedPenalties = 500m }
    ];
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name != nameof(ITicketRepository.GetReportSummaryAsync))
            throw new InvalidOperationException($"Unexpected ticket call: {method?.Name}");
        Calls++;
        Start = (DateTime)args![0]!;
        End = (DateTime)args[1]!;
        Type = (ViolationType?)args[2];
        return Task.FromResult(Rows.Where(x => !Type.HasValue || x.Type == Type).ToList());
    }
}

internal sealed class MemoryReportRepository : IReportRepository
{
    public List<Report> Items { get; } = [];
    public Task AddAsync(Report report) { report.Id = Items.Count + 1; Items.Add(report); return Task.CompletedTask; }
    public Task<Report?> GetByIdAsync(int id) => Task.FromResult(Items.SingleOrDefault(x => x.Id == id));
    public Task<List<Report>> GetHistoryAsync(int offset, int limit) => Task.FromResult(Items
        .OrderByDescending(x => x.DateGenerated).ThenByDescending(x => x.Id).Skip(offset).Take(limit + 1).ToList());
    public Task<int> GetCountAsync() => Task.FromResult(Items.Count);
}

internal sealed class ReportUnitOfWork : IUnitOfWork
{
    public int Begins { get; private set; }
    public int Saves { get; private set; }
    public int Commits { get; private set; }
    public int Rollbacks { get; private set; }
    public bool FailSave { get; set; }
    public Task BeginTransactionAsync() { Begins++; return Task.CompletedTask; }
    public Task<int> SaveChangesAsync() { Saves++; if (FailSave) throw new InvalidOperationException("Save failed"); return Task.FromResult(1); }
    public Task CommitAsync() { Commits++; return Task.CompletedTask; }
    public Task RollbackAsync() { Rollbacks++; return Task.CompletedTask; }
    public void Dispose() { }
}
