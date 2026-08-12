using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
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
                    LastName = v.User!.LastName,
                    FirstName = v.User.FirstName,
                    MarketSectionId = v.MarketSectionId,
                    MarketSectionName = v.MarketSection!.Name,
                    BusinessName = v.BusinessName,
                    StallNumber = v.StallNumber
                })
                .FirstOrDefaultAsync();
        }

        public async Task<WarningCheck> CheckHasWarning(int vendorId)
        {
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            var warningTicket = await _context.Tickets
                .Where(t => t.VendorId == vendorId
                    && t.Type == ViolationType.Warning
                    && t.Status == TicketStatus.Active
                    && t.IssuedAt >= sevenDaysAgo)
                .FirstOrDefaultAsync();

            return new WarningCheck
            {
                VendorId = vendorId,
                CanIssueWarning = warningTicket == null,
                ActiveWarningIssuedAt = warningTicket?.IssuedAt
            };
        }

        public async Task<List<WarningCheck>> CheckHasWarningList(List<int> vendorIds)
        {
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

            var activeWarnings = await _context.Tickets
                .Where(t => vendorIds.Contains(t.VendorId)
                    && t.Type == ViolationType.Warning
                    && t.Status == TicketStatus.Active
                    && t.IssuedAt >= sevenDaysAgo)
                .ToListAsync();

            return vendorIds.Select(id =>
            {
                var warning = activeWarnings.FirstOrDefault(t => t.VendorId == id);
                return new WarningCheck
                {
                    VendorId = id,
                    CanIssueWarning = warning is null,
                    ActiveWarningIssuedAt = warning?.IssuedAt
                };
            }).ToList();
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

        public async Task<List<VendorLookupResult>> GetVendorByStallNumber(string stallNumber)
        {
            return await _context.VendorProfiles
                .Where(v => v.StallNumber == stallNumber && v.Status == VendorStatus.Active)
                .Select(v => new VendorLookupResult
                {
                    VendorId = v.Id,
                    Username = v.User.UserName,
                    StallNumber = v.StallNumber,
                    TradeName = v.BusinessName,
                    LastName = v.User.LastName,
                    FirstName = v.User.FirstName,
                    MiddleName = v.User.MiddleName,
                    Address = "",
                    MarketSectionId = v.MarketSectionId,
                    MarketSectionName = v.MarketSection.Name
                })
                .ToListAsync();
        }

        public async Task<VendorLookupResult?> GetVendorByQrCode(string codeValue)
        {
            return await _context.VendorProfiles
                .Where(v => v.QrCodeValue == codeValue && v.Status == VendorStatus.Active)
                .Select(v => new VendorLookupResult
                {
                    VendorId = v.Id,
                    Username = v.User.UserName,
                    StallNumber = v.StallNumber,
                    TradeName = v.BusinessName,
                    LastName = v.User.LastName,
                    FirstName = v.User.FirstName,
                    MiddleName = v.User.MiddleName,
                    Address = "",
                    MarketSectionId = v.MarketSectionId,
                    MarketSectionName = v.MarketSection.Name
                })
                .FirstOrDefaultAsync();
        }
    }
}
