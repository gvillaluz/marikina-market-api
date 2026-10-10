using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using MarikinaMarket.API.Application;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;

namespace MarikinaMarket.API.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context; // Your actual DbContext class name
        private IDbContextTransaction? _currentTransaction;
        private readonly AuditLogContext? _audit;

        public UnitOfWork(AppDbContext context, AuditLogContext? audit = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _audit = audit;
        }

        public async Task BeginTransactionAsync()
        {
            if (_currentTransaction != null)
            {
                return;
            }

            _currentTransaction = await _context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable
            );
        }

        public async Task CommitAsync()
        {
            try
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.CommitAsync();
                    if (_audit?.Staged == true) _audit.Stored = true;
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                await RollbackAsync();
                throw new ConcurrencyConflictException("The record was changed by another request. Refresh and try again.");
            }
            catch
            {
                await RollbackAsync();
                throw;
            }
            finally
            {
                await DisposeTransactionAsync();
            }
        }

        public async Task RollbackAsync()
        {
            try
            {
                if (_currentTransaction != null)
                {
                    await _currentTransaction.RollbackAsync();
                }
            }
            finally
            {
                if (_currentTransaction is not null && _audit is not null)
                {
                    _audit.Staged = false;
                    _audit.Stored = false;
                }
                await DisposeTransactionAsync();
            }
        }

        public async Task<int> SaveChangesAsync()
        {
            try
            {
                return await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConcurrencyConflictException("The record was changed by another request. Refresh and try again.");
            }
        }

        private async Task DisposeTransactionAsync()
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }

        public void Dispose()
        {
            _currentTransaction?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
