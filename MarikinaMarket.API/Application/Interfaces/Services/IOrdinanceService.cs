using MarikinaMarket.API.Application.DTOs.Ordinance.Response;

namespace MarikinaMarket.API.Application.Interfaces.Services
{
    public interface IOrdinanceService
    {
        public Task<List<GetOrdinanceResponse>> GetOrdinances();
    }
}
