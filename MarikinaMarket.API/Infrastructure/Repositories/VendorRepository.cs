using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Application.DTOs.Vendor.Request;
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
                    Email = v.User!.Email!,
                    MarketSectionId = v.MarketSectionId,
                    MarketSectionName = v.MarketSection!.Name,
                    BusinessName = v.BusinessName,
                    BusinessId = v.BusinessId,
                    StallNumber = v.StallNumber,
                    Type = v.Type
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
                .Include(v => v.MarketSection)
                .Include(v => v.ReviewedByUser)
                .FirstOrDefaultAsync(v => v.Id == registrationId);
        }

        public async Task<bool> RegistrationExistsAsync(string email, string businessId)
        {
            var normalizedEmail = email.ToLower();
            var normalizedBusinessId = businessId.ToLower();

            return await _context.VendorRegistrationRequests.AnyAsync(request =>
                    request.Email.ToLower() == normalizedEmail ||
                    request.BusinessId.ToLower() == normalizedBusinessId)
                || await _context.VendorProfiles.AnyAsync(vendor =>
                    vendor.BusinessId.ToLower() == normalizedBusinessId);
        }

        public async Task<VendorRegistrationStatusCounts> GetVendorRegistrationStatusCountsAsync()
        {
            var counts = await _context.VendorRegistrationRequests
                .AsNoTracking()
                .GroupBy(request => 1)
                .Select(group => new VendorRegistrationStatusCounts
                {
                    PendingReview = group.Count(request => request.Status == RequestStatus.Pending),
                    NeedsInformation = group.Count(request => request.Status == RequestStatus.NeedsInformation),
                    Approved = group.Count(request => request.Status == RequestStatus.Approved),
                    Rejected = group.Count(request => request.Status == RequestStatus.Rejected)
                })
                .FirstOrDefaultAsync();

            return counts ?? new VendorRegistrationStatusCounts();
        }

        public async Task<List<VendorRegistrationSummary>> GetVendorRegistrationSummariesAsync(
            int offset,
            int pageSize,
            VendorRegistrationRequestFilters filters)
        {
            var requests = ApplyVendorRegistrationFilters(
                _context.VendorRegistrationRequests.AsNoTracking(),
                filters);

            return await requests
                .OrderByDescending(request => request.RequestedAt)
                .ThenByDescending(request => request.Id)
                .Skip(offset)
                .Take(pageSize)
                .Select(request => new VendorRegistrationSummary
                {
                    RegistrationId = request.Id,
                    BusinessId = request.BusinessId,
                    BusinessName = request.BusinessName,
                    VendorName = request.LastName + ", " + request.FirstName,
                    VendorType = request.Type,
                    MarketSectionName = request.MarketSection!.Name,
                    StallNumber = request.StallNumber,
                    RequestedAt = request.RequestedAt,
                    Status = request.Status
                })
                .ToListAsync();
        }

        public async Task<int> GetVendorRegistrationSummaryCountAsync(
            VendorRegistrationRequestFilters filters)
        {
            return await ApplyVendorRegistrationFilters(
                    _context.VendorRegistrationRequests.AsNoTracking(),
                    filters)
                .CountAsync();
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

        public async Task<List<VendorLookupResult>> GetVendorByBusinessId(string businessId)
        {
            return await _context.VendorProfiles
                .Where(v => v.BusinessId.ToLower().Contains(businessId.ToLower()) && v.Status == VendorStatus.Active)
                .Select(v => new VendorLookupResult
                {
                    VendorId = v.Id,
                    Username = v.User!.UserName!,
                    Type = v.Type,
                    BusinessId = v.BusinessId,
                    StallNumber = v.StallNumber,
                    TradeName = v.BusinessName,
                    LastName = v.User.LastName,
                    FirstName = v.User.FirstName,
                    MiddleName = v.User.MiddleName,
                    Address = "",
                    MarketSectionId = v.MarketSectionId,
                    MarketSectionName = v.MarketSection!.Name
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
                    Username = v.User!.UserName!,
                    Type = v.Type,
                    BusinessId = v.BusinessId,
                    StallNumber = v.StallNumber,
                    TradeName = v.BusinessName,
                    LastName = v.User.LastName,
                    FirstName = v.User.FirstName,
                    MiddleName = v.User.MiddleName,
                    Address = "",
                    MarketSectionId = v.MarketSectionId,
                    MarketSectionName = v.MarketSection!.Name
                })
                .FirstOrDefaultAsync();
        }

        public async Task<VendorProfileDetails?> GetVendorProfileDetailsAsync(int vendorId)
        {
            return await _context.VendorProfiles
                .AsNoTracking()
                .Where(v => v.Id == vendorId)
                .Select(v => new VendorProfileDetails
                {
                    FirstName = v.User!.FirstName,
                    MiddleName = v.User.MiddleName,
                    LastName = v.User.LastName,
                    Username = v.User.UserName,
                    PhoneNumber = v.User.PhoneNumber,
                    Email = v.User.Email,
                    AccountCreatedAt = v.User.CreatedAt,
                    LastViolationIssuedAt = v.Tickets
                        .Where(t => t.Type == ViolationType.Ticket)
                        .OrderByDescending(t => t.IssuedAt)
                        .Select(t => (DateTime?)t.IssuedAt)
                        .FirstOrDefault()
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<VendorProfile>> GetVendorProfilesForComplianceUpdateAsync()
            => await _context.VendorProfiles.OrderBy(v => v.Id).ToListAsync();

        public async Task<VendorProfile?> GetVendorProfileForUpdateAsync(int vendorId)
        {
            return await _context.VendorProfiles
                .FirstOrDefaultAsync(v => v.Id == vendorId);
        }

        public async Task<List<AdminVendorSummary>> GetAdminVendorSummariesAsync(
            int offset,
            int pageSize,
            AdminVendorSummaryFilter filters)
        {
            var vendors = ApplyAdminVendorFilters(_context.VendorProfiles.AsNoTracking(), filters);

            return await vendors
                .OrderBy(v => v.BusinessId)
                .Skip(offset)
                .Take(pageSize)
                .Select(v => new AdminVendorSummary
                {
                    VendorId = v.Id,
                    BusinessId = v.BusinessId,
                    VendorName = v.User!.LastName + ", " + v.User.FirstName,
                    VendorType = v.Type,
                    MarketSectionName = v.MarketSection!.Name,
                    ComplianceScore = v.ComplianceScore,
                    WarningCount = v.Tickets.Count(t => t.Type == ViolationType.Warning),
                    TicketCount = v.Tickets.Count(t => t.Type == ViolationType.Ticket)
                })
                .ToListAsync();
        }

        public async Task<int> GetAdminVendorSummaryCountAsync(AdminVendorSummaryFilter filters)
        {
            return await ApplyAdminVendorFilters(_context.VendorProfiles.AsNoTracking(), filters)
                .CountAsync();
        }

        public async Task<int> GetVendorWarningCountThisWeekAsync(DateTime weekStart)
        {
            return await _context.Tickets
                .Where(t => t.Type == ViolationType.Warning && t.IssuedAt >= weekStart)
                .Select(t => t.VendorId)
                .Distinct()
                .CountAsync();
        }

        public async Task<int> GetVendorTicketCountThisWeekAsync(DateTime weekStart)
        {
            return await _context.Tickets
                .Where(t => t.Type == ViolationType.Ticket && t.IssuedAt >= weekStart)
                .Select(t => t.VendorId)
                .Distinct()
                .CountAsync();
        }

        public async Task<List<AdminVendorActivity>> GetRecentVendorActivitiesAsync(int limit)
        {
            return await _context.Tickets
                .AsNoTracking()
                .OrderByDescending(t => t.IssuedAt)
                .Take(limit)
                .Select(t => new AdminVendorActivity
                {
                    TicketId = t.Id,
                    ControlNumber = t.ControlNumber,
                    BusinessId = t.Vendor!.BusinessId,
                    FirstName = t.Vendor.User!.FirstName,
                    MiddleName = t.Vendor.User.MiddleName,
                    LastName = t.Vendor.User.LastName,
                    Type = t.Type,
                    IssuedAt = t.IssuedAt
                })
                .ToListAsync();
        }

        public async Task<List<AdminPendingTicketSettlement>> GetPendingTicketSettlementsAsync(
            int offset,
            int pageSize)
        {
            return await _context.Tickets
                .AsNoTracking()
                .Where(t => t.Type == ViolationType.Ticket &&
                    (t.Status == TicketStatus.Pending || t.Status == TicketStatus.Overdue))
                .OrderByDescending(t => t.IssuedAt)
                .Skip(offset)
                .Take(pageSize)
                .Select(t => new AdminPendingTicketSettlement
                {
                    TicketId = t.Id,
                    ControlNumber = t.ControlNumber,
                    BusinessId = t.Vendor!.BusinessId,
                    VendorName = t.Vendor.User!.LastName + ", " + t.Vendor.User.FirstName,
                    MarketSectionName = t.MarketSection!.Name,
                    Status = t.Status ?? TicketStatus.Pending,
                    PenaltyType = t.PenaltyType,
                    TotalPaymentAmount = t.TotalPaymentAmount,
                    CommunityServiceHours = t.CommunityServiceHours,
                    IssuedAt = t.IssuedAt,
                    DueDate = t.IssuedAt.AddDays(15)
                })
                .ToListAsync();
        }

        public async Task<int> GetPendingTicketSettlementCountAsync()
        {
            return await _context.Tickets
                .CountAsync(t => t.Type == ViolationType.Ticket &&
                    (t.Status == TicketStatus.Pending || t.Status == TicketStatus.Overdue));
        }

        private static IQueryable<VendorProfile> ApplyAdminVendorFilters(
            IQueryable<VendorProfile> vendors,
            AdminVendorSummaryFilter filters)
        {
            if (filters.MarketSectionId.HasValue)
                vendors = vendors.Where(v => v.MarketSectionId == filters.MarketSectionId.Value);

            if (filters.ComplianceScoreRange is not null)
            {
                vendors = filters.ComplianceScoreRange switch
                {
                    "85-100" => vendors.Where(v => v.ComplianceScore >= 85 && v.ComplianceScore <= 100),
                    "70-84" => vendors.Where(v => v.ComplianceScore >= 70 && v.ComplianceScore <= 84),
                    "50-69" => vendors.Where(v => v.ComplianceScore >= 50 && v.ComplianceScore <= 69),
                    "Below 50" => vendors.Where(v => v.ComplianceScore >= 0 && v.ComplianceScore < 50),
                    _ => vendors
                };
            }

            if (!string.IsNullOrWhiteSpace(filters.Search))
            {
                var search = filters.Search.Trim().ToLower();
                vendors = vendors.Where(v =>
                    v.BusinessId.ToLower().Contains(search) ||
                    v.BusinessName.ToLower().Contains(search) ||
                    v.User!.FirstName.ToLower().Contains(search) ||
                    v.User.LastName.ToLower().Contains(search));
            }

            return vendors;
        }

        private static IQueryable<VendorRegistrationRequest> ApplyVendorRegistrationFilters(
            IQueryable<VendorRegistrationRequest> requests,
            VendorRegistrationRequestFilters filters)
        {
            if (filters.Status.HasValue)
                requests = requests.Where(request => request.Status == filters.Status.Value);

            if (filters.VendorType.HasValue)
                requests = requests.Where(request => request.Type == filters.VendorType.Value);

            return requests;
        }

        public void SetOriginalVersion(VendorRegistrationRequest registration, uint version)
        {
            _context.Entry(registration)
                .Property(r => r.Version)
                .OriginalValue = version;
        }
    }
}
