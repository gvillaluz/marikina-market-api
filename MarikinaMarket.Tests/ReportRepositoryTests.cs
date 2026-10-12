using System.Linq.Expressions;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Infrastructure.Persistence;
using MarikinaMarket.API.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace MarikinaMarket.Tests;

public class ReportRepositoryTests
{
    [Fact]
    public void ReportMigrationMatchesModelAndGeneratesPostgresSqlOffline()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .UseSnakeCaseNamingConvention().Options);
        Assert.False(context.Database.HasPendingModelChanges());
        var sql = context.GetService<IMigrator>().GenerateScript("20261010113144_UpdatedAuditLog");
        Assert.Contains("CREATE TABLE reports", sql);
        Assert.Contains("numeric(18,2)", sql);
        Assert.Contains("ON DELETE RESTRICT", sql);
    }

    [Fact]
    public async Task AggregatesEveryStatusAndFiltersIssueDateBoundaries()
    {
        var start = new DateTime(2026, 10, 11, 16, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(1);
        var records = new List<Ticket>
        {
            Record(ViolationType.Warning, null, null, start),
            Record(ViolationType.Ticket, TicketStatus.Paid, 100m, start),
            Record(ViolationType.Ticket, TicketStatus.Waived, 200m, start.AddHours(1)),
            Record(ViolationType.Ticket, TicketStatus.Cleared, null, start.AddHours(2)),
            Record(ViolationType.Ticket, TicketStatus.Pending, 300m, start.AddHours(3)),
            Record(ViolationType.Ticket, TicketStatus.Overdue, 400m, start.AddHours(4)),
            Record(ViolationType.Ticket, TicketStatus.Contested, 500m, start.AddHours(5)),
            Record(ViolationType.Ticket, TicketStatus.InProgress, null, start.AddHours(6)),
            Record(ViolationType.Ticket, null, null, start.AddHours(7)),
            Record(ViolationType.Ticket, TicketStatus.Paid, 999m, start.AddTicks(-1)),
            Record(ViolationType.Warning, null, null, end)
        };
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
        // Replace only the database boundary; execute the repository's actual LINQ expression.
        var set = new ReportTestDbSet<Ticket>(records);
        context.Tickets = set;
        var repository = new TicketRepository(context);
        var result = await repository.GetReportSummaryAsync(start, end, null);
        var warning = Assert.Single(result, x => x.Type == ViolationType.Warning);
        Assert.True(warning.IsResolved);
        Assert.Equal(1, warning.TotalRecords);
        Assert.Equal(0m, warning.AssessedPenalties);
        var resolved = Assert.Single(result, x => x.Type == ViolationType.Ticket && x.IsResolved);
        Assert.Equal(3, resolved.TotalRecords);
        Assert.Equal(300m, resolved.AssessedPenalties);
        var pending = Assert.Single(result, x => x.Type == ViolationType.Ticket && !x.IsResolved);
        Assert.Equal(5, pending.TotalRecords);
        Assert.Equal(1200m, pending.AssessedPenalties);

        // Translate that same expression with the real PostgreSQL provider, without opening a connection.
        using var postgres = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .UseSnakeCaseNamingConvention().Options);
        var expression = new ReplaceReportQueryRoot(set.RootExpression,
            ((IQueryable<Ticket>)postgres.Tickets).Expression).Visit(set.ProviderState.LastExpression!);
        var sql = ((IQueryable<Ticket>)postgres.Tickets).Provider.CreateQuery(expression!).ToQueryString();
        Assert.Contains("GROUP BY", sql);
        Assert.Contains("sum(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("issued_at", sql);

        var tickets = await repository.GetReportSummaryAsync(start, end, ViolationType.Ticket);
        Assert.All(tickets, row => Assert.Equal(ViolationType.Ticket, row.Type));
        var warnings = await repository.GetReportSummaryAsync(start, end, ViolationType.Warning);
        Assert.Single(warnings);
    }

    private static Ticket Record(ViolationType type, TicketStatus? status, decimal? amount, DateTime issuedAt)
        => new() { Type = type, Status = status, TotalPaymentAmount = amount, IssuedAt = issuedAt, Description = "Market inspection" };
}

internal sealed class ReplaceReportQueryRoot(Expression source, Expression target) : ExpressionVisitor
{
    public override Expression? Visit(Expression? node) => node == source ? target : base.Visit(node);
}

internal sealed class ReportQueryState
{
    public Expression? LastExpression { get; set; }
}

internal sealed class ReportTestDbSet<T> : DbSet<T>, IQueryable<T> where T : class
{
    public override Microsoft.EntityFrameworkCore.Metadata.IEntityType EntityType => throw new NotSupportedException();
    private readonly IQueryable<T> _source;
    public ReportQueryState ProviderState { get; } = new();
    public Expression RootExpression => _source.Expression;
    public ReportTestDbSet(IEnumerable<T> items) => _source = items.AsQueryable();
    Type IQueryable.ElementType => typeof(T);
    Expression IQueryable.Expression => _source.Expression;
    IQueryProvider IQueryable.Provider => new ReportTestQueryProvider(_source.Provider, ProviderState);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => _source.GetEnumerator();
}

internal sealed class ReportTestQueryProvider(IQueryProvider inner, ReportQueryState state) : IAsyncQueryProvider
{
    public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();
    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        state.LastExpression = expression;
        return new ReportTestAsyncQuery<TElement>(expression, state);
    }
    public object? Execute(Expression expression) => inner.Execute(expression);
    public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);
    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

internal sealed class ReportTestAsyncQuery<T>(Expression expression, ReportQueryState state)
    : EnumerableQuery<T>(expression), IQueryable<T>, IAsyncEnumerable<T>
{
    IQueryProvider IQueryable.Provider => new ReportTestQueryProvider(
        ((IQueryable<T>)new EnumerableQuery<T>(((IQueryable<T>)this).Expression)).Provider, state);
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new ReportTestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());
}

internal sealed class ReportTestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
{
    public T Current => inner.Current;
    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
    public ValueTask DisposeAsync() { inner.Dispose(); return ValueTask.CompletedTask; }
}
