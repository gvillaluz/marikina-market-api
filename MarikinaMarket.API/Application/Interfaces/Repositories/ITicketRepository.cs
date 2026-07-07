using MarikinaMarket.API.Application.DTOs.Ordinance.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Interfaces.Repositories
{
    public interface ITicketRepository
    {
        public Task<int> GetNewControlNumber();
        public Task<Ticket> AddTicketAsync(Ticket ticket);
        public Task<List<Ticket>> GetAllTicketsByEnforcerId(int enforcerId);
        public Task SaveChangesAsync();
    }
}
