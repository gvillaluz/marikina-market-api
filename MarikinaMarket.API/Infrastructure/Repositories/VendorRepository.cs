using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MarikinaMarket.API.Infrastructure.Repositories
{
    public class VendorRepository : IVendorRepository
    {
        private readonly AppDbContext _context;

        public VendorRepository(AppDbContext context) => _context = context;

        public async Task<VendorTicketSummary?> GetByIdAsync(int id)
        {
            return await _context.VendorProfiles
                .Where(u => u.Id == id)
                .Select(v => new VendorTicketSummary
                {
                    Id = v.Id,
                    MarketSectionId = v.MarketSectionId,
                    MarketSectionName = v.MarketSection!.Name,
                    BusinessName = v.BusinessName
                })
                .FirstOrDefaultAsync();
        }

        public async Task<VendorProfile> CreateVendorAsync(VendorProfile vendorProfile)
        {
            await _context.VendorProfiles.AddAsync(vendorProfile);
            return vendorProfile;
        }

        public async Task<VendorRegistrationRequest> AddVendorRegistryAsync(VendorRegistrationRequest vendor)
        {
            await _context.VendorRegistrationRequests.AddAsync(vendor);
            return vendor;
        }

        public async Task<VendorRegistrationRequest?> GetRegistrationById(int registrationId)
        {
            return await _context.VendorRegistrationRequests
                .FirstOrDefaultAsync(v => v.Id == registrationId);
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
