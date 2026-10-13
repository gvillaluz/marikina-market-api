using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface IReportRepository
    {
        Task AddAsync(Report report);
        Task<Report?> GetByIdAsync(int id);
        Task<List<Report>> GetHistoryAsync(int offset, int limit);
        Task<int> GetCountAsync();
    }
}
