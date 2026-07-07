using MarikinaMarket.API.Application.DTOs.Ordinance.Internal;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarikinaMarket.API.Infrastructure.Repositories
{
    public class TicketRepository : ITicketRepository
    {
        private readonly AppDbContext _context;

        public TicketRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<int> GetNewControlNumber()
        {
            var lastTicket = await _context.Tickets
                .FromSqlRaw("SELECT * FROM tickets ORDER BY id DESC LIMIT 1 FOR UPDATE")
                .FirstOrDefaultAsync();

            if (lastTicket == null) return 1;

            return int.Parse(lastTicket.ControlNumber) + 1;
        }

        public async Task<Ticket> AddTicketAsync(Ticket ticket)
        {
            await _context.Tickets.AddAsync(ticket);

            return ticket;
        }

        public async Task<List<Ticket>> GetAllTicketsByEnforcerId(int enforcerId)
        {
            return await _context.Tickets
                .Where(t => t.EnforcerId == enforcerId)
                .OrderByDescending(t => t.IssuedAt)
                .Take(10)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine(ex.Message);
                throw new Exception("A database error occured while saving the changes.");
            }
        }
    }
}
