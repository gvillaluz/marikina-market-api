using System.Reflection;
using MarikinaMarket.API.Application;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;
using MarikinaMarket.API.Infrastructure.Persistence;
using MarikinaMarket.API.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Xunit;

namespace MarikinaMarket.Tests;

public class UnitOfWorkConcurrencyTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task SerializationAndRowConflictsUseTheExistingConflictResponse(int shape, bool commit)
    {
        var postgres = new PostgresException("Concurrent write", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure);
        Exception error = shape switch
        {
            0 => postgres,
            1 => new DbUpdateException("Save failed", postgres),
            2 => new InvalidOperationException("Provider execution failed", new DbUpdateException("Save failed", postgres)),
            _ => new DbUpdateConcurrencyException("Stale row")
        };
        using var context = new FailingContext(error);
        var audit = new AuditLogContext { Staged = true, Stored = true };
        using var unit = new UnitOfWork(context, audit);
        var transaction = new FailingTransaction(error);
        typeof(UnitOfWork).GetField("_currentTransaction", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(unit, transaction);

        var conflict = await Assert.ThrowsAsync<ConcurrencyConflictException>(() => commit ? unit.CommitAsync() : unit.SaveChangesAsync());
        Assert.Equal("The record was changed by another request. Refresh and try again.", conflict.Message);
        if (!commit) await unit.RollbackAsync();
        Assert.True(transaction.RolledBack);
        Assert.Equal(1, transaction.DisposeCount);
        Assert.False(audit.Staged);
        Assert.False(audit.Stored);
        await unit.RollbackAsync();
        Assert.Equal(1, transaction.DisposeCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UnrelatedProviderAndOperationErrorsAreNotReclassified(bool providerError, bool commit)
    {
        Exception error = providerError
            ? new InvalidOperationException("Provider execution failed", new DbUpdateException("Save failed",
                new PostgresException("Unique violation", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation)))
            : new InvalidOperationException("Unrelated operation failure");
        using var context = new FailingContext(error);
        using var unit = new UnitOfWork(context);
        var transaction = new FailingTransaction(error);
        typeof(UnitOfWork).GetField("_currentTransaction", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(unit, transaction);

        var actual = await Record.ExceptionAsync(() => commit ? unit.CommitAsync() : unit.SaveChangesAsync());
        Assert.Same(error, actual);
        if (!commit) await unit.RollbackAsync();
        Assert.True(transaction.RolledBack);
        Assert.Equal(1, transaction.DisposeCount);
    }

    private sealed class FailingContext(Exception error) : AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromException<int>(error);
    }

    private sealed class FailingTransaction(Exception error) : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public bool RolledBack { get; private set; }
        public int DisposeCount { get; private set; }
        public void Commit() => throw error;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.FromException(error);
        public void Rollback() => RolledBack = true;
        public Task RollbackAsync(CancellationToken cancellationToken = default) { Rollback(); return Task.CompletedTask; }
        public void Dispose() => DisposeCount++;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
