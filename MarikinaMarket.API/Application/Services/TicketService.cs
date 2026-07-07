using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.IdentityModel.Tokens.Experimental;

namespace MarikinaMarket.API.Application.Services
{
    public class TicketService : ITicketService
    {
        private readonly ITicketRepository _ticketRepository;
        private readonly IOrdinanceRepository _ordinanceRepository;
        private readonly IVendorRepository _vendorRepository;
        private readonly IUnitOfWork _unitOfWork;

        public TicketService(
            ITicketRepository ticketRepository,
            IOrdinanceRepository ordinanceRepository,
            IVendorRepository vendorRepository,
            IUnitOfWork unitOfWork
            )
        {
            _ticketRepository = ticketRepository;
            _ordinanceRepository = ordinanceRepository;
            _vendorRepository = vendorRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<FineSummaryResponse> GetOffenseCountsAndPaymentBy(List<int> ordinanceIds, int vendorId)
        {
            var ordinanceWithTiers = await _ordinanceRepository.GetByIdsAsync(ordinanceIds, vendorId);

            List<OrdinanceFineBreakdownItem> items = [];

            decimal totalPaymentAmount = 0;

            Severity highestSeverity = Severity.Low;

            foreach (var ordinance in ordinanceWithTiers)
            {
                ordinance.OffenseCount += 1;

                var applicableTier = ordinance.PenaltyTiers
                    .FirstOrDefault(pt => pt.OffenseNumber == ordinance.OffenseCount)
                    ?? ordinance.PenaltyTiers.OrderByDescending(pt => pt.OffenseNumber).First();

                totalPaymentAmount += applicableTier.PenaltyAmount;

                if (applicableTier.Severity > highestSeverity)
                    highestSeverity = applicableTier.Severity;

                items.Add(new OrdinanceFineBreakdownItem
                {
                    OrdinanceId = ordinance.OrdinanceId,
                    OffenseNumber = ordinance.OffenseCount,
                    PaymentAmount = applicableTier.PenaltyAmount,
                    Severity = applicableTier.Severity
                });
            }

            return new FineSummaryResponse
            {
                TotalPaymentAmount = totalPaymentAmount,
                HighestSeverity = highestSeverity,
                Breakdown = items
            };
        }

        public async Task<TicketDetailResponse> CreateTicketAsync(CreateTicketRequest request)
        {
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                var vendor = await _vendorRepository.GetByIdAsync(request.VendorId);

                if (vendor is null)
                    throw new ArgumentNullException("Unable to find vendor.");

                var ordinanceFineSummary = await GetOffenseCountsAndPaymentBy(request.Ordinances, request.VendorId);

                bool isCashFine = request.PenaltyType == PenaltyType.CashFine;

                if (!isCashFine && ordinanceFineSummary.HighestSeverity == Severity.High)
                    throw new InvalidOperationException("High severity violations must be paid as cash fine.");

                var paymentStatus = isCashFine ? PaymentStatus.Pending
                                        : request.PenaltyType == PenaltyType.BloodDonation
                                            ? PaymentStatus.BloodDonation
                                            : PaymentStatus.CommunityService;

                var newControlNumber = await _ticketRepository.GetNewControlNumber();

                var ticket = new Ticket
                {
                    ControlNumber = newControlNumber.ToString(),
                    VendorId = vendor.Id,
                    MarketSectionId = vendor.MarketSectionId,
                    EnforcerId = request.EnforcerId,
                    Type = request.Type,
                    Status = TicketStatus.Active,
                    Description = request.Description,
                    TotalPaymentAmount = ordinanceFineSummary.TotalPaymentAmount,
                    HighestSeverity = ordinanceFineSummary.HighestSeverity,
                    PenaltyType = request.PenaltyType,
                    PaymentStatus = paymentStatus,
                    CommunityServiceHours = request.CommunityServiceHours,
                    ReceiptUrl = null,
                    PrimaryCategory = request.PrimaryCategory,
                    IssuedAt = DateTime.UtcNow,
                    TicketViolations = ordinanceFineSummary.Breakdown
                                            .Select(o => new TicketViolation
                                            {
                                                OrdinanceId = o.OrdinanceId,
                                                OffenseCount = o.OffenseNumber,
                                                PenaltyAmount = o.PaymentAmount
                                            }).ToList(),
                    TicketEvidences = request.TicketEvidenceUrls
                                            .Select(te => new TicketEvidence
                                            {
                                                EvidenceUrl = te.Url,
                                                CapturedAt = te.CapturedAt
                                            }).ToList()
                };

                var newTicket = await _ticketRepository.AddTicketAsync(ticket);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                return new TicketDetailResponse
                {
                    Id = newTicket.Id,
                    ControlNumber = newTicket.ControlNumber,
                    VendorId = newTicket.VendorId,
                    MarketSectionId = newTicket.MarketSectionId,
                    MarketSectionName = vendor.MarketSectionName!,
                    BusinessName = vendor.BusinessName!,
                    EnforcerId = newTicket.EnforcerId,
                    Type = newTicket.Type,
                    Status = newTicket.Status,
                    IssuedAt = newTicket.IssuedAt,
                    IsOverdue = newTicket.IssuedAt.AddDays(15) > DateTime.UtcNow
                };
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<List<TicketDetailResponse>> GetAllByEnforcerId(int enforcerId)
        {
            var tickets = await _ticketRepository.GetAllTicketsByEnforcerId(enforcerId);

            if (tickets is null)
                throw new ArgumentNullException("Failed to load tickets");

            var ticketResponse = tickets.Select(t => new TicketDetailResponse
            {
                Id = t.Id,
                ControlNumber = t.ControlNumber,
                VendorId = t.VendorId,
                BusinessName = t.Vendor!.BusinessName,
                MarketSectionId = t.MarketSectionId,
                MarketSectionName = t.MarketSection!.Name,
                EnforcerId = t.EnforcerId,
                Type = t.Type,
                Status = t.Status,
                IssuedAt = t.IssuedAt,
                IsOverdue = t.IssuedAt.AddDays(15) > DateTime.UtcNow,
                UpdatedAt = t.UpdatedAt
            }).ToList();

            return ticketResponse;
        }
    }
}
