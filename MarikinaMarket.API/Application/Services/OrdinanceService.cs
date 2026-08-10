using MarikinaMarket.API.Application.DTOs.Ordinance.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Services
{
    public class OrdinanceService : IOrdinanceService
    {
        private readonly IOrdinanceRepository _repository;

        public OrdinanceService(IOrdinanceRepository repository) => _repository = repository;

        public async Task<List<GetOrdinanceResponse>> GetOrdinances()
        {
            var ordinances = await _repository.GetOrdinances();

            return ordinances
                .Select(o => new GetOrdinanceResponse
                {
                    Id = o.Id,
                    OrdinanceNo = o.OrdinanceNo,
                    Code = o.Code,
                    Title = o.Title,
                    Description = o.Description,
                    Category = o.Category,
                    Severity = o.PenaltyTiers
                            .FirstOrDefault(pt => pt.OffenseNumber == 1)?.Severity
                            ?? o.PenaltyTiers.OrderBy(pt => pt.OffenseNumber).First().Severity,
                    CreatedAt = o.CreatedAt
                }).ToList();
        }
    }
}
