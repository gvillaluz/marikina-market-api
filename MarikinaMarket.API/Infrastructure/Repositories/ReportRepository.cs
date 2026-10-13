using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarikinaMarket.API.Infrastructure.Repositories
{
    public class ReportRepository : IReportRepository
    {
        private readonly AppDbContext _context;
        public ReportRepository(AppDbContext context) => _context = context;

        public async Task AddAsync(Report report) => await _context.Reports.AddAsync(report);
        public Task<Report?> GetByIdAsync(int id) => _context.Reports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        public Task<int> GetCountAsync() => _context.Reports.CountAsync();
        public Task<List<Report>> GetHistoryAsync(int offset, int limit) => _context.Reports.AsNoTracking()
            .OrderByDescending(x => x.DateGenerated).ThenByDescending(x => x.Id)
            .Skip(offset).Take(limit + 1).ToListAsync();
    }
}
