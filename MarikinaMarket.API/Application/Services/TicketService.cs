using System.Diagnostics;
using MarikinaMarket.API.Application.DTOs.Ordinance.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MarikinaMarket.API.Application.Services
{
    public class TicketService : ITicketService
    {
        private readonly ITicketRepository _ticketRepository;
        private readonly IOrdinanceRepository _ordinanceRepository;
        private readonly IVendorRepository _vendorRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationService _notificationService;
        private readonly IStorageService _storageService;
        private readonly int PAGE_SIZE = 10;

        public TicketService(
            ITicketRepository ticketRepository,
            IOrdinanceRepository ordinanceRepository,
            IVendorRepository vendorRepository,
            IUnitOfWork unitOfWork,
            IStorageService storageService,
            INotificationService notificationService
            )
        {
            _ticketRepository = ticketRepository;
            _ordinanceRepository = ordinanceRepository;
            _vendorRepository = vendorRepository;
            _unitOfWork = unitOfWork;
            _storageService = storageService;
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
            Severity highestSeverity = Severity.Minor;

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

                var stopwatch = Stopwatch.StartNew();
                var vendor = await _vendorRepository.GetByIdAsync(request.VendorId);

                if (vendor is null)
                    throw new RecordNotFoundException("Unable to find vendor.");

                var duplicateOrdinances = await _ticketRepository.GetDuplicatedTickets(vendor.Id, request.Ordinances);

                if (duplicateOrdinances.Any())
                {
                    foreach (var ordinance in duplicateOrdinances) Console.WriteLine("Removing ordinance id:" + ordinance);
                    request.Ordinances.RemoveAll(o => duplicateOrdinances.Select(o => o.OrdinanceId).Contains(o));
                }

                if (!request.Ordinances.Any())
                {
                    throw new DuplicateOrdinanceException(
                        "All selected ordinance(s) already have active tickets issued for this vendor today.",
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
                        Status = null,
                        Description = request.Description,
                        TotalPaymentAmount = null,
                        HighestSeverity = null,
                        PenaltyType = null,
                        CommunityServiceHours = null,
                        ReceiptUrls = [],
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

                    if (newWarningTicket is null)
                        throw new ResourceCreationFailedException("Failed to create the ticket. Please try again.");

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
                        Status = newWarningTicket!.Status,
                        IssuedAt = newWarningTicket.IssuedAt,
                        OverdueDate = null,
                        UpdatedAt = newWarningTicket.UpdatedAt,
                        DuplicateOrdinances = [],
                        WarningMessageForDuplicates = null
                    };
                }

                var newControlNumber = await _ticketRepository.GetNewControlNumber();
                var ticketEvidences = new List<TicketEvidence>();

                if (request.TicketEvidenceFiles != null && request.TicketEvidenceFiles.Any())
                {
                    Dictionary<IFormFile, string> filesWithKeys = new Dictionary<IFormFile, string>();

                    foreach (var file in request.TicketEvidenceFiles)
                    {
                        Console.WriteLine(
                            $"Evidence: {file.FileName} | " +
                            $"{file.Length / 1024.0 / 1024.0:F2} MB | " +
                            $"{file.ContentType}"
                        );
                        filesWithKeys[file] = GenerateFileKey(file);
                    }

                    var keys = await _storageService.UploadEvidencesAsync(filesWithKeys);
                    Console.WriteLine($"Image upload: {stopwatch.ElapsedMilliseconds} ms");

                    foreach (var key in keys)
                    {
                        ticketEvidences.Add(new TicketEvidence
                        {
                            FileKey = key
                        });
                    }
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
                    TotalPaymentAmount = isCashFine ? ordinanceFineSummary.TotalPaymentAmount : null,
                    HighestSeverity = ordinanceFineSummary.HighestSeverity,
                    PenaltyType = request.PenaltyType,
                    CommunityServiceHours = request.CommunityServiceHours,
                    ReceiptUrls = [],
                    Categories = [.. ordinanceFineSummary.Breakdown.Select(o => o.Category)],
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

                Console.WriteLine($"Commit: {stopwatch.ElapsedMilliseconds} ms");

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
                Status = t.Status ?? TicketStatus.Pending,
                IssuedAt = t.IssuedAt,
                UpdatedAt = t.UpdatedAt,
                OverdueDate = t.IssuedAt.AddDays(15)
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

            if (ticketDetail is null)
                throw new RecordNotFoundException("Ticket not found.");

            if (ticketDetail.TicketEvidences!.Any())
            {
                Dictionary<string, string> keysWithUrls = await _storageService.GetPresignedUrlsAsync(
                    B2BucketType.Evidence,
                    ticketDetail.TicketEvidences!
                );

                var presignedUrls = new List<string>();

                foreach (var key in ticketDetail.TicketEvidences!)
                {
                    presignedUrls.Add(keysWithUrls[key]);
                }

                ticketDetail.TicketEvidences = presignedUrls;
            }

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

            await _notificationService.SaveNotificationAsync(ticket.Id, ticket.EnforcerId, $"{ticket.Vendor?.BusinessName} — status changed from {previousStatus} to {ticket.Status}.", ticket.Status ?? TicketStatus.Pending);

            return new UpdateStatusResponse
            {
                TicketId = ticket.Id,
                ControlNumber = ticket!.ControlNumber!,
                Status = ticket.Status ?? TicketStatus.Cleared,
                UpdatedAt = ticket.UpdatedAt,
                Version = ticket.Version
            };
        }

        public async Task<PageResponse<AdminInspectionSummaryResponse>> GetAdminInspectionAsync(int offset, InspectionSummaryFilters filters)
        {
            offset = Math.Max(offset, 0);

            var inspections = await _ticketRepository.GetAdminInspectionAsync(offset, PAGE_SIZE, filters);

            if (inspections is null || inspections.Count == 0)
                return new PageResponse<AdminInspectionSummaryResponse> { Items = [], HasMore = false };

            bool hasMore = inspections.Count() > PAGE_SIZE;
            if (hasMore)
                inspections.RemoveAt(inspections.Count - 1);

            var totalCount = await _ticketRepository.GetTotalTicketCountAsync(null);

            var inspectionResponse = inspections.Select(t => new AdminInspectionSummaryResponse
            {
                TicketId = t.Id,
                EnforcerId = t.EnforcerId,
                EnforcerLastName = t.EnforcerLastName,
                EnforcerFirstName = t.EnforcerFirstName,
                VendorId = t.VendorId,
                VendorLastName = t.VendorLastName,
                VendorFirstName = t.VendorFirstName,
                StallNumber = t.StallNumber,
                BusinessName = t.BusinessName,
                MarketSectionId = t.MarketSectionId,
                MarketSectionName = t.MarketSectionName,
                Type = t.Type,
                IssuedAt = t.IssuedAt
            }).ToList();

            return new PageResponse<AdminInspectionSummaryResponse>
            {
                Items = inspectionResponse,
                HasMore = hasMore,
                Total = totalCount
            };
        }

        public async Task<PageResponse<AdminTicketSummary>> GetAdminTicketAsync(int offset, TicketSummaryFilters filters)
        {
            offset = Math.Max(offset, 0);

            var inspections = await _ticketRepository.GetAdminTicketAsync(offset, PAGE_SIZE, filters);

            if (inspections is null || inspections.Count == 0)
                return new PageResponse<AdminTicketSummary> { Items = [], HasMore = false };

            bool hasMore = inspections.Count() > PAGE_SIZE;
            if (hasMore)
                inspections.RemoveAt(inspections.Count - 1);

            var totalCount = await _ticketRepository.GetTotalTicketCountAsync(ViolationType.Ticket);

            var inspectionResponse = inspections.Select(t => new AdminTicketSummary
            {
                Id = t.Id,
                ControlNumber = t.ControlNumber,
                EnforcerId = t.EnforcerId,
                EnforcerLastName = t.EnforcerLastName,
                EnforcerFirstName = t.EnforcerFirstName,
                VendorId = t.VendorId,
                VendorLastName = t.VendorLastName,
                VendorFirstName = t.VendorFirstName,
                StallNumber = t.StallNumber,
                MarketSectionId = t.MarketSectionId,
                MarketSectionName = t.MarketSectionName,
                Status = t.Status,
                Severity = t.Severity,
                PenaltyType = t.PenaltyType,
                TotalPaymentAmount = t.TotalPaymentAmount,
                IssuedAt = t.IssuedAt
            }).ToList();

            return new PageResponse<AdminTicketSummary>
            {
                Items = inspectionResponse,
                HasMore = hasMore,
                Total = totalCount
            };
        }

        public async Task<TicketAnalyticsResponse> GetTicketAnalyticsAsync()
        {
            var now = DateTime.UtcNow;
            var startOfThisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var startOfLastMonth = startOfThisMonth.AddMonths(-1);

            var rawAnalytics = await _ticketRepository.GetTicketAnalyticsAsync(startOfThisMonth, startOfLastMonth);

            return new TicketAnalyticsResponse
            {
                TotalTicketsThisMonth = rawAnalytics.TotalTicketsThisMonth,
                TicketChangePercentage = CalculatePercentChange(rawAnalytics.TotalTicketsLastMonth, rawAnalytics.TotalTicketsThisMonth) ?? 0,

                PendingPaymentsThisMonth = rawAnalytics.PaymentsThisMonth,
                PaymentsChangePercentage = rawAnalytics.PaymentsLastMonth == 0
                    ? 0
                    : Math.Round((double)((rawAnalytics.PaymentsThisMonth - rawAnalytics.PaymentsLastMonth)
                        / rawAnalytics.PaymentsLastMonth) * 100, 1),

                ResolvedViolationsThisMonth = rawAnalytics.ResolvedViolationsThisMonth,
                ResolutionRate = rawAnalytics.TotalTicketsThisMonth == 0
                    ? 0
                    : Math.Round((double)rawAnalytics.ResolvedViolationsThisMonth / rawAnalytics.TotalTicketsThisMonth * 100, 1),

                HighSeveritiesThisMonth = rawAnalytics.HighSeveritiesThisMonth,
                HighSeveritiesChangePercentage = CalculatePercentChange(rawAnalytics.HighSeveritiesLastMonth, rawAnalytics.HighSeveritiesThisMonth) ?? 0
            };
        }

        public async Task<AdminTicketDetailResponse> GetAdminTicketDetailAsync(int ticketId)
        {
            var ticket = await _ticketRepository.GetAdminTicketDetailAsync(ticketId);

            if (ticket is null) throw new RecordNotFoundException("Ticket not found");

            if (ticket.TicketEvidences!.Any())
            {
                Dictionary<string, string> keysWithUrls = await _storageService.GetPresignedUrlsAsync(
                    B2BucketType.Evidence,
                    ticket.TicketEvidences!
                );

                var presignedUrls = new List<string>();

                foreach (var key in ticket.TicketEvidences!)
                {
                    presignedUrls.Add(keysWithUrls[key]);
                }

                ticket.TicketEvidences = presignedUrls;
            }

            return ticket;
        }

        public async Task<int> CheckAndNotifyOverdueTicketsAsync()
        {
            var newlyOverdueTickets = await _ticketRepository.GetNewlyOverdueTicketsAsync();

            foreach (var ticket in newlyOverdueTickets)
            {
                var previousStatus = ticket.Status;
                ticket.Status = TicketStatus.Overdue;
                ticket.UpdatedAt = DateTime.UtcNow;

                await _notificationService.SendPushNotificationAsync(
                    ticket.EnforcerId,
                    $"Ticket #{ticket.ControlNumber} Overdue",
                    $"{ticket.Vendor?.BusinessName}'s ticket is now overdue. Payment was due 15 days ago."
                );

                await _notificationService.SaveNotificationAsync(
                    ticket.Id, 
                    ticket.EnforcerId, 
                    $"{ticket.Vendor?.BusinessName} — status changed from {previousStatus} to {ticket.Status}.", ticket.Status ?? TicketStatus.Pending
                );

                if (!string.IsNullOrEmpty(ticket.Vendor?.User?.Email))
                {
                    string subject = $"Overdue Notice - Control No. {ticket.ControlNumber}";
                    string body = $"Dear {ticket.Vendor.User.FirstName} {ticket.Vendor.User.LastName},\n\n" +
                        $"This is to inform you that your ticket has now become overdue.\n\n" +
                        $"Control No.: {ticket.ControlNumber}\n" +
                        $"Total Penalty: PHP {ticket.TotalPaymentAmount:N2}\n" +
                        $"Original Due Date: {ticket.IssuedAt.AddDays(15):MMMM dd, yyyy}\n\n" +
                        $"Please settle your outstanding balance as soon as possible to avoid further administrative action, " +
                        $"including additional penalties or suspension of your stall permit.\n\n" +
                        $"If you wish to contest this ticket, please visit the market administration office to file an appeal.\n\n" +
                        $"Thank you for your prompt attention to this matter.\n\n" +
                        $"Sincerely,\n" +
                        $"Market Administration Office";

                    await _notificationService.SendEmailAsync(ticket.Vendor.User.Email, subject, body);
                }
            }

            if (newlyOverdueTickets.Count > 0)
                await _ticketRepository.SaveChangesAsync();

            return newlyOverdueTickets.Count;
        }

        private static double? CalculatePercentChange(int previous, int current)
        {
            if (previous == 0) return null;
            return Math.Round((double)(current - previous) / previous * 100, 1);
        }

        private static string GenerateFileKey(IFormFile file)
        {
            string fileExtension = Path.GetExtension(file.FileName);
            string datePath = DateTime.UtcNow.ToString("yyyy/MM");
            return $"tickets/{datePath}/{Guid.NewGuid()}{fileExtension}";
        }
    }
}
