using MarikinaMarket.API.Application.DTOs.Ordinance.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens.Experimental;
using Microsoft.OpenApi;

namespace MarikinaMarket.API.Application.Services
{
    public class TicketService : ITicketService
    {
        private readonly ITicketRepository _ticketRepository;
        private readonly IOrdinanceRepository _ordinanceRepository;
        private readonly IVendorRepository _vendorRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorage _fileStorage;
        private readonly INotificationService _notificationService;
        private readonly int PAGE_SIZE = 10;

        public TicketService(
            ITicketRepository ticketRepository,
            IOrdinanceRepository ordinanceRepository,
            IVendorRepository vendorRepository,
            IUnitOfWork unitOfWork,
            IFileStorage fileStorage,
            INotificationService notificationService
            )
        {
            _ticketRepository = ticketRepository;
            _ordinanceRepository = ordinanceRepository;
            _vendorRepository = vendorRepository;
            _unitOfWork = unitOfWork;
            _fileStorage = fileStorage;
            _notificationService = notificationService;
        }

        public async Task<MobileDashboardSummaryResponse> GetMobileTicketCountAsync(int enforcerId)
        {
            var ticketCount = await _ticketRepository.GetTicketCountAsync(enforcerId);

            return new MobileDashboardSummaryResponse
            {
                TicketRecorded = ticketCount.TicketRecorded,
                WarningRecorded = ticketCount.WarningRecorded,
                TotalRecorded = ticketCount.TotalRecorded  
            };
        }

        public async Task<FineSummaryResponse> GetOffenseCountsAndPaymentBy(List<int> ordinanceIds, int vendorId)
        {
            var ordinanceWithTiers = await _ordinanceRepository.GetByIdsAsync(ordinanceIds, vendorId);
            var duplicateOrdinances = await _ticketRepository.GetDuplicatedTickets(vendorId, ordinanceIds);
            return BuildFineSummary(ordinanceWithTiers, false, duplicateOrdinances);
        }

        private static FineSummaryResponse BuildFineSummary(
            List<OrdinanceOffenseSummary> ordinances,
            bool persist,
            List<DuplicateOrdinance>? duplicateOrdinances = null
            )
        {
            List<OrdinanceFineBreakdownItem> items = [];
            decimal totalPaymentAmount = 0;
            Severity highestSeverity = Severity.Low;

            List<int> duplicateIds = [];

            if (duplicateOrdinances != null && duplicateOrdinances.Any())
            {
                duplicateIds = duplicateOrdinances.Select(d => d.OrdinanceId).ToList();
            }

            foreach (var ordinance in ordinances)
            {
                int offenseNumber = persist 
                    ? ordinance.OffenseCount + 1 
                    : ordinance.OffenseCount <= 0 
                        ? 1
                        : ordinance.OffenseCount;
                bool IsDuplicate = duplicateIds.Contains(ordinance.OrdinanceId);

                if (persist && !IsDuplicate)
                    ordinance.OffenseCount = offenseNumber;

                var applicableTier = ordinance.PenaltyTiers
                    .FirstOrDefault(pt => pt.OffenseNumber == offenseNumber)
                    ?? ordinance.PenaltyTiers.OrderByDescending(pt => pt.OffenseNumber).First();

                if (!IsDuplicate)
                {
                    totalPaymentAmount += applicableTier.PenaltyAmount;

                    if (applicableTier.Severity > highestSeverity)  
                        highestSeverity = applicableTier.Severity;
                }

                items.Add(new OrdinanceFineBreakdownItem
                {
                    OrdinanceId = ordinance.OrdinanceId,
                    OrdinanceNo = ordinance.OrdinanceNo,
                    OrdinanceCode = ordinance.Code,
                    OffenseNumber = offenseNumber,
                    PaymentAmount = applicableTier.PenaltyAmount,
                    Severity = applicableTier.Severity,
                    Category = ordinance.Category,
                    IsDuplicate = IsDuplicate
                });
            }

            return new FineSummaryResponse
            {
                TotalPaymentAmount = totalPaymentAmount,
                HighestSeverity = highestSeverity,
                Breakdown = items
            };
        }

        public async Task<InspectionSummaryResponse> CreateTicketAsync(CreateTicketRequest request)
        {
            await _unitOfWork.BeginTransactionAsync();

            try
            {
                var vendor = await _vendorRepository.GetByIdAsync(request.VendorId);

                if (vendor is null)
                    throw new RecordNotFoundException("Unable to find vendor.");

                var duplicateOrdinances = await _ticketRepository.GetDuplicatedTickets(vendor.Id, request.Ordinances);

                if (duplicateOrdinances.Any())
                {
                    foreach (var ordinance in  duplicateOrdinances) Console.WriteLine("Removing ordinance id:" + ordinance);
                    request.Ordinances.RemoveAll(o => duplicateOrdinances.Select(o => o.OrdinanceId).Contains(o));
                }

                if (!request.Ordinances.Any())
                {
                    throw new DuplicateOrdinanceException(
                        "All selected ordinances already have active tickets issued for this vendor today.",
                        duplicateOrdinances
                    );
                }

                var ordinanceWithTiers = await _ordinanceRepository.GetByIdsAsync(request.Ordinances, request.VendorId);

                var ordinanceFineSummary = BuildFineSummary(
                    ordinanceWithTiers,
                    persist: request.Type != ViolationType.Warning
                );

                if (request.Type == ViolationType.Warning)
                {
                    var hasActiveWarningTicket = await _ticketRepository.HasActiveWarningTicket(vendor.Id);

                    if (hasActiveWarningTicket)
                    {
                        throw new DuplicateWarningException("This vendor already has an active warning. A second warning cannot be issued within 24 hours.");
                    }

                    var warningTicket = new Ticket
                    {
                        ControlNumber = null,
                        VendorId = vendor.Id,
                        MarketSectionId = vendor.MarketSectionId,
                        EnforcerId = request.EnforcerId,
                        Type = request.Type,
                        Status = TicketStatus.Pending,
                        Description = request.Description,
                        TotalPaymentAmount = null,
                        HighestSeverity = null,
                        PenaltyType = null,
                        CommunityServiceHours = null,
                        ReceiptUrl = null,
                        Categories = [.. ordinanceFineSummary.Breakdown.Select(o => o.Category)],
                        IssuedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        TicketViolations = ordinanceFineSummary.Breakdown
                                            .Select(o => new TicketViolation
                                            {
                                                OrdinanceId = o.OrdinanceId,
                                                OffenseCount = o.OffenseNumber,
                                                PenaltyAmount = null
                                            }).ToList(),
                        TicketEvidences = []
                    };

                    var newWarningTicket = await _ticketRepository.AddTicketAsync(warningTicket);

                    await _unitOfWork.SaveChangesAsync();
                    await _unitOfWork.CommitAsync();

                    if (!string.IsNullOrEmpty(vendor.Email))
                    {
                        string subject = $"Notice of Issued Warning";
                        string body = $"Dear {vendor.FirstName} {vendor.LastName},\n\n" +
                            $"This is to inform you that a warning ticket has been issued for your stall, {vendor.BusinessName}.\n\n" +
                            $"Reason for Warning:\n{newWarningTicket.Description}\n\n" +
                            $"Date Issued: {newWarningTicket.IssuedAt:MMMM dd, yyyy - hh:mm tt} UTC\n\n" +
                            $"This warning serves as an official notice. Please address the issue(s) mentioned above promptly. " +
                            $"Failure to resolve them may result in a formal citation and corresponding fines.\n\n" +
                            $"If you believe this warning was issued in error, you may contact the market administration office to file a dispute.\n\n" +
                            $"Thank you for your cooperation.\n\n" +
                            $"Sincerely,\n" +
                            $"Market Administration Office";

                        await _notificationService.SendEmailAsync(vendor.Email, subject, body);
                    }

                    return new InspectionSummaryResponse
                    {
                        Id = newWarningTicket.Id,
                        ControlNumber = newWarningTicket!.ControlNumber!,
                        VendorId = newWarningTicket.VendorId,
                        LastName = vendor.LastName,
                        FirstName = vendor.FirstName,
                        MarketSectionId = newWarningTicket.MarketSectionId,
                        MarketSectionName = vendor.MarketSectionName!,
                        StallNumber = vendor.StallNumber,
                        BusinessName = vendor.BusinessName!,
                        EnforcerId = newWarningTicket.EnforcerId,
                        Type = newWarningTicket.Type,
                        Severity = null,
                        OrdinanceNames = ordinanceFineSummary.Breakdown.Select(o => o.OrdinanceNo).ToList(),
                        Status = newWarningTicket.Status,
                        IssuedAt = newWarningTicket.IssuedAt,
                        OverdueDate = null,
                        IsOverdue = false,
                        UpdatedAt = newWarningTicket.UpdatedAt,
                        DuplicateOrdinances = [],
                        WarningMessageForDuplicates = null
                    };
                }

                var newControlNumber = await _ticketRepository.GetNewControlNumber();
                var ticketEvidences = new List<TicketEvidence>();

                foreach (var file in request.TicketEvidenceFiles)
                {
                    string url = await _fileStorage.SaveFileAsync(file, "evidences");
                    ticketEvidences.Add(new TicketEvidence
                    {
                        EvidenceUrl = url
                    });
                }

                bool isCashFine = request.PenaltyType == PenaltyType.CashFine;

                if (!isCashFine && ordinanceFineSummary.HighestSeverity == Severity.High)
                    throw new InvalidOperationException("High severity violations must be paid as cash fine.");

                var ticket = new Ticket
                {
                    ControlNumber = newControlNumber.ToString(),
                    VendorId = vendor.Id,
                    MarketSectionId = vendor.MarketSectionId,
                    EnforcerId = request.EnforcerId,
                    Type = request.Type,
                    Status = TicketStatus.Pending,
                    Description = request.Description,
                    TotalPaymentAmount = ordinanceFineSummary.TotalPaymentAmount,
                    HighestSeverity = ordinanceFineSummary.HighestSeverity,
                    PenaltyType = request.PenaltyType,
                    CommunityServiceHours = request.CommunityServiceHours,
                    ReceiptUrl = null,
                    Categories = [..ordinanceFineSummary.Breakdown.Select(o => o.Category)],
                    IssuedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    TicketViolations = ordinanceFineSummary.Breakdown
                                            .Select(o => new TicketViolation
                                            {
                                                OrdinanceId = o.OrdinanceId,
                                                OffenseCount = o.OffenseNumber,
                                                PenaltyAmount = o.PaymentAmount
                                            }).ToList(),
                    TicketEvidences = ticketEvidences
                };

                var newTicket = await _ticketRepository.AddTicketAsync(ticket);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();

                if (!string.IsNullOrEmpty(vendor.Email))
                {
                    string subject = $"Notice of Violation Ticket - Control No. {newTicket.ControlNumber}";
                    string body = $"Dear {vendor.FirstName} {vendor.LastName},\n\n" +
                        $"This is to inform you that a violation ticket has been issued for your stall, {vendor.BusinessName}.\n\n" +
                        $"Control No.: {newTicket.ControlNumber}\n" +
                        $"Total Penalty: PHP {newTicket.TotalPaymentAmount:N2}\n" +
                        $"Due Date: {newTicket.IssuedAt.AddDays(15):MMMM dd, yyyy}\n\n" +
                        $"Please settle the amount above on or before the due date to avoid further administrative action, including additional penalties or suspension of your stall permit.\n\n" +
                        $"If you wish to contest this ticket, please visit the market administration office within 15 days of the issue date to file an appeal.\n\n" +
                        $"Thank you for your prompt attention to this matter.\n\n" +
                        $"Sincerely,\n" +
                        $"Market Administration Office";

                    await _notificationService.SendEmailAsync(vendor.Email, subject, body);
                }

                return new InspectionSummaryResponse
                {
                    Id = newTicket.Id,
                    ControlNumber = newTicket!.ControlNumber!,
                    VendorId = newTicket.VendorId,
                    LastName = vendor.LastName,
                    FirstName = vendor.FirstName,
                    MarketSectionId = newTicket.MarketSectionId,
                    MarketSectionName = vendor.MarketSectionName!,
                    StallNumber = vendor.StallNumber,
                    BusinessName = vendor.BusinessName!,
                    EnforcerId = newTicket.EnforcerId,
                    Type = newTicket.Type,
                    Severity = newTicket.HighestSeverity,
                    OrdinanceNames = ordinanceFineSummary.Breakdown.Select(o => o.OrdinanceNo).ToList(),
                    Status = newTicket.Status,
                    IssuedAt = newTicket.IssuedAt,
                    IsOverdue = DateTime.UtcNow > newTicket.IssuedAt.AddDays(15),
                    UpdatedAt = newTicket.UpdatedAt,
                    DuplicateOrdinances = duplicateOrdinances,
                    WarningMessageForDuplicates = $"{duplicateOrdinances.Count} ordinance(s) were excluded as active tickets already exist."
                };
            }
            catch (Exception)
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<PageResponse<InspectionSummaryResponse>> GetInspectionsByEnforcerIdAsync(int enforcerId, int offset, ViolationType type)
        {
            offset = Math.Max(offset, 0);

            var tickets = await _ticketRepository.GetInspectionsAsync(enforcerId, offset, PAGE_SIZE, type);

            if (tickets is null || tickets.Count == 0)
                return new PageResponse<InspectionSummaryResponse> { Items = [], HasMore = false };

            var hasMore = tickets.Count > PAGE_SIZE;
            if (hasMore)
                tickets.RemoveAt(tickets.Count - 1);

            var ticketResponse = tickets.Select(t => new InspectionSummaryResponse
            {
                Id = t.Id,
                ControlNumber = t.ControlNumber,
                VendorId = t.VendorId,
                LastName = t.LastName,
                FirstName = t.FirstName,
                BusinessName = t.BusinessName,
                MarketSectionId = t.MarketSectionId,
                MarketSectionName = t.MarketSectionName,
                StallNumber = t.StallNumber,
                EnforcerId = t.EnforcerId,
                Type = t.Type,
                Status = t.Status,
                Severity = t.Severity,
                OrdinanceNames = t.Ordinances,
                IssuedAt = t.IssuedAt,
                OverdueDate = t.Type == ViolationType.Ticket
                    ? t.IssuedAt.AddDays(15)
                    : null,
                IsOverdue = t.Type == ViolationType.Ticket
                    ? t.IssuedAt.AddDays(15) < DateTime.UtcNow
                    : false,
                UpdatedAt = t.UpdatedAt
            }).ToList();

            return new PageResponse<InspectionSummaryResponse>
            {
                Items = ticketResponse,
                HasMore = hasMore
            };
        }

        public async Task<PageResponse<TicketSummaryResponse>> GetTicketsByEnforcerIdAsync(
            int enforcerId, 
            int offset, 
            TicketStatus status
            )
        {
            offset = Math.Max(offset, 0);

            var tickets = await _ticketRepository.GetTicketsAsync(enforcerId, offset, PAGE_SIZE, status);

            if (tickets is null || tickets.Count == 0)
                return new PageResponse<TicketSummaryResponse> { Items = [], HasMore = false };

            var hasMore = tickets.Count > PAGE_SIZE;
            if (hasMore)
                tickets.RemoveAt(tickets.Count - 1);

            var ticketResponse = tickets.Select(t => new TicketSummaryResponse
            {
                Id = t.Id,
                ControlNumber = t.ControlNumber,
                VendorId = t.VendorId,
                BusinessName = t.BusinessName,
                MarketSectionName = t.MarketSectionName,
                StallNumber = t.StallNumber,
                EnforcerId = t.EnforcerId,
                Status = t.Status,
                IssuedAt = t.IssuedAt,
                IsOverdue = t.IssuedAt.AddDays(15) < DateTime.UtcNow,
                UpdatedAt = t.UpdatedAt
            }).ToList();

            return new PageResponse<TicketSummaryResponse>
            {
                Items = ticketResponse,
                HasMore = hasMore
            };
        }

        public async Task<TicketDetailResponse> GetTicketDetailByIdAsync(int ticketId)
        {
            var ticketDetail = await _ticketRepository.GetTicketDetailAsync(ticketId);

            if (ticketDetail == null)
                throw new RecordNotFoundException("Ticket not found.");

            return ticketDetail;
        }

        public async Task<UpdateStatusResponse> UpdateTicketStatusAsync(int ticketId, UpdateStatusRequest request)
        {
            var ticket = await _ticketRepository.GetTicketByIdAsync(ticketId);

            if (ticket is null) throw new RecordNotFoundException("Ticket not found.");

            var previousStatus = ticket.Status;

            _ticketRepository.SetOriginalVersion(ticket, request.Version);
            ticket.Status = request.NewStatus;
            ticket.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _ticketRepository.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConcurrencyConflictException("The ticket has been updated by another user. Please refresh and try again.");
            }

            if (!string.IsNullOrEmpty(ticket?.Vendor?.User?.Email))
            {
                string subject = $"Ticket Status Update - Control No. {ticket.ControlNumber}";
                string body = $"Dear {ticket.Vendor.User.FirstName} {ticket.Vendor.User.LastName},\n\n" +
                    $"This is to inform you that the status of your ticket has been updated.\n\n" +
                    $"Control No.: {ticket.ControlNumber}\n" +
                    $"Previous Status: {previousStatus}\n" +
                    $"Current Status: {ticket.Status}\n" +
                    $"Updated At: {ticket.UpdatedAt:MMMM dd, yyyy - hh:mm tt} UTC\n\n" +
                    $"Please review your ticket details for further information. If you have any questions or concerns regarding this update, please visit the market administration office.\n\n" +
                    $"Thank you for your attention to this matter.\n\n" +
                    $"Sincerely,\n" +
                    $"Market Administration Office";

                await _notificationService.SendEmailAsync(ticket.Vendor.User.Email, subject, body);
            }

            await _notificationService.SendPushNotificationAsync(
                ticket!.EnforcerId,
                $"Ticket #{ticket.ControlNumber} Updated",
                $"{ticket.Vendor?.BusinessName} — status changed from {previousStatus} to {ticket.Status}."
            );

            return new UpdateStatusResponse
            {
                TicketId = ticket.Id,
                ControlNumber = ticket!.ControlNumber!,
                Status = ticket.Status,
                UpdatedAt = ticket.UpdatedAt,
                Version = ticket.Version
            };
        }
    }
}
