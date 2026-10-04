using System.Diagnostics;
using System.Net;
using MarikinaMarket.API.Application.DTOs.Ordinance.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Application.DTOs.Vendor.Request;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;

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
        private readonly IVendorComplianceScoreService _complianceScoreService;
        private readonly int PAGE_SIZE = 10;

        public TicketService(
            ITicketRepository ticketRepository,
            IOrdinanceRepository ordinanceRepository,
            IVendorRepository vendorRepository,
            IUnitOfWork unitOfWork,
            IStorageService storageService,
            INotificationService notificationService,
            IVendorComplianceScoreService complianceScoreService
            )
        {
            _ticketRepository = ticketRepository;
            _ordinanceRepository = ordinanceRepository;
            _vendorRepository = vendorRepository;
            _unitOfWork = unitOfWork;
            _storageService = storageService;
            _notificationService = notificationService;
            _complianceScoreService = complianceScoreService;
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

        public async Task<TicketReceiptProofResponse> GetTicketReceiptProofAsync(int ticketId, int enforcerId)
        {
            var ticket = await GetTicketForSettlementAsync(ticketId, enforcerId);

            if (ticket.PenaltyType is not (PenaltyType.CashFine or PenaltyType.BloodDonation))
                throw new InvalidRequestException("Receipt proof is only available for cash fine or blood donation tickets.");

            return await BuildTicketReceiptProofResponseAsync(ticket);
        }

        public async Task<CommunityServiceProgressResponse> GetCommunityServiceProgressAsync(int ticketId, int enforcerId)
        {
            var ticket = await GetTicketForSettlementAsync(ticketId, enforcerId);

            if (ticket.PenaltyType != PenaltyType.CommunityService)
                throw new InvalidRequestException("Community service logs are only available for community service tickets.");

            return await BuildCommunityServiceProgressAsync(ticket);
        }

        public async Task<TicketReceiptProofResponse> SubmitTicketReceiptProofAsync(
            int ticketId,
            int enforcerId,
            SubmitTicketReceiptProofRequest request)
        {
            var ticket = await GetTicketForSettlementAsync(ticketId, enforcerId);

            if (ticket.PenaltyType is not (PenaltyType.CashFine or PenaltyType.BloodDonation))
                throw new InvalidRequestException("Receipt proof is only accepted for cash fine or blood donation tickets.");

            var proofKey = await UploadSettlementProofAsync(request.ProofFile);

            ticket.ProofUrls ??= [];
            ticket.ProofUrls.Add(proofKey);
            ticket.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();

            return await BuildTicketReceiptProofResponseAsync(ticket);
        }

        public async Task<CommunityServiceProgressResponse> LogCommunityServiceHoursAsync(
            int ticketId,
            int enforcerId,
            SubmitCommunityServiceLogRequest request)
        {
            var ticket = await GetTicketForSettlementAsync(ticketId, enforcerId);

            if (ticket.PenaltyType != PenaltyType.CommunityService)
                throw new InvalidRequestException("Community service entries are only accepted for community service tickets.");

            ValidateCommunityServiceLog(ticket, request);

            var completedHours = ticket.CommunityServiceLogs.Sum(log => log.HoursWorked);
            var requiredHours = ticket.CommunityServiceHours!.Value;
            var hoursWorked = request.HoursWorked!.Value;

            if (completedHours + hoursWorked > requiredHours)
            {
                var remainingHours = requiredHours - completedHours;
                throw new InvalidRequestException($"Hours worked cannot exceed the remaining {remainingHours:0.##} community service hours.");
            }

            var proofKey = await UploadSettlementProofAsync(request.ProofFile);

            ticket.CommunityServiceLogs.Add(new CommunityServiceLog
            {
                TicketId = ticket.Id,
                ServiceDate = DateTime.SpecifyKind(request.ServiceDate!.Value.Date, DateTimeKind.Utc),
                HoursWorked = hoursWorked,
                ProofUrl = proofKey,
                RecordedById = enforcerId,
                CreatedAt = DateTime.UtcNow
            });
            ticket.UpdatedAt = DateTime.UtcNow;
            ticket.Status = completedHours + hoursWorked >= requiredHours
                ? TicketStatus.Cleared
                : TicketStatus.InProgress;
            ticket.ResolvedAt = ticket.Status == TicketStatus.Cleared
                ? ticket.UpdatedAt
                : null;
            await _unitOfWork.SaveChangesAsync();

            return await BuildCommunityServiceProgressAsync(ticket);
        }

        private async Task<TicketReceiptProofResponse> BuildTicketReceiptProofResponseAsync(Ticket ticket)
        {
            var proofKeys = ticket.ProofUrls ?? [];
            var proofUrls = await _storageService.GetPresignedUrlsAsync(B2BucketType.Evidence, proofKeys);

            return new TicketReceiptProofResponse
            {
                TicketId = ticket.Id,
                PenaltyType = ticket.PenaltyType!.Value,
                ProofUrls = proofKeys.Select(key => proofUrls[key]).ToList()
            };
        }

        private async Task<CommunityServiceProgressResponse> BuildCommunityServiceProgressAsync(Ticket ticket)
        {
            if (!ticket.CommunityServiceHours.HasValue || ticket.CommunityServiceHours.Value <= 0)
                throw new InvalidRequestException("The ticket does not have a valid community service hour requirement.");

            var logs = ticket.CommunityServiceLogs
                .OrderBy(log => log.ServiceDate)
                .ThenBy(log => log.CreatedAt)
                .ToList();
            var proofUrls = await _storageService.GetPresignedUrlsAsync(B2BucketType.Evidence, logs.Select(log => log.ProofUrl));
            var requiredHours = ticket.CommunityServiceHours.Value;
            var completedHours = logs.Sum(log => log.HoursWorked);

            return new CommunityServiceProgressResponse
            {
                TicketId = ticket.Id,
                HoursRequired = requiredHours,
                HoursCompleted = completedHours,
                HoursRemaining = requiredHours - completedHours,
                CompletionPercentage = Math.Min(100m, Math.Round(completedHours / requiredHours * 100m, 1)),
                Entries = logs.Select(log => new CommunityServiceLogResponse
                {
                    ServiceDate = log.ServiceDate,
                    HoursWorked = log.HoursWorked,
                    ProofUrl = proofUrls[log.ProofUrl]
                }).ToList()
            };
        }

        private static void ValidateCommunityServiceLog(
            Ticket ticket,
            SubmitCommunityServiceLogRequest request)
        {
            if (!request.ServiceDate.HasValue)
                throw new InvalidRequestException("Service date is required.");

            if (request.ServiceDate.Value.Date > DateTime.UtcNow.Date)
                throw new InvalidRequestException("Service date cannot be in the future.");

            if (!request.HoursWorked.HasValue
                || request.HoursWorked.Value < 0.01m
                || request.HoursWorked.Value > 999.99m)
                throw new InvalidRequestException("Hours worked must be between 0.01 and 999.99.");

            if (decimal.Round(request.HoursWorked.Value, 2) != request.HoursWorked.Value)
                throw new InvalidRequestException("Hours worked must not have more than two decimal places.");

            if (!ticket.CommunityServiceHours.HasValue || ticket.CommunityServiceHours.Value <= 0)
                throw new InvalidRequestException("The ticket does not have a valid community service hour requirement.");
        }

        private async Task<Ticket> GetTicketForSettlementAsync(int ticketId, int enforcerId)
        {
            var ticket = await _ticketRepository.GetTicketByIdAsync(ticketId);

            if (ticket is null)
                throw new RecordNotFoundException("Ticket not found.");

            if (ticket.EnforcerId != enforcerId)
                throw new UnauthorizedAccessException("You can only submit settlement proof for tickets assigned to you.");

            if (ticket.Type != ViolationType.Ticket)
                throw new InvalidRequestException("Settlement proof can only be submitted for violation tickets.");

            if (!ticket.PenaltyType.HasValue)
                throw new InvalidRequestException("The ticket does not have a penalty type.");

            return ticket;
        }

        private async Task<string> UploadSettlementProofAsync(IFormFile? file)
        {
            ValidateSettlementProof(file);

            var proofFile = file!;
            var proofKey = GenerateFileKey(proofFile);
            await _storageService.UploadEvidenceAsync(proofFile, proofKey);

            return proofKey;
        }

        public async Task<FineSummaryResponse> GetOffenseCountsAndPaymentBy(List<int> ordinanceIds, int vendorId)
        {
            var ordinanceWithTiers = await _ordinanceRepository.GetByIdsAsync(ordinanceIds, vendorId);
            var duplicateOrdinances = await _ticketRepository.GetDuplicatedTickets(vendorId, ordinanceIds);
            return BuildFineSummary(ordinanceWithTiers, false, duplicateOrdinances);
        }

        public async Task<List<WarningOrdinance>> GetWarningOrdinancesForVendorAsync(
            List<int> ordinanceIds,
            int vendorId)
        {
            return await _ticketRepository.GetWarningOrdinancesForVendor(vendorId, ordinanceIds);
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
                var isDuplicate = duplicateIds.Contains(ordinance.OrdinanceId);

                int offenseNumber = ordinance.OffenseCount + 1;

                if (persist && !isDuplicate)
                    ordinance.OffenseCount = offenseNumber;

                var applicableTier = ordinance.PenaltyTiers
                    .FirstOrDefault(pt => pt.OffenseNumber == offenseNumber)
                    ?? ordinance.PenaltyTiers.OrderByDescending(pt => pt.OffenseNumber).First();

                if (!isDuplicate)
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
                    IsDuplicate = isDuplicate
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
                    persist: request.Type == ViolationType.Ticket
                );

                if (request.Type == ViolationType.Warning)
                {
                    var warnedOrdinances = await GetWarningOrdinancesForVendorAsync(request.Ordinances, vendor.Id);

                    if (warnedOrdinances.Any())
                    {
                        throw new DuplicateWarningException("A warning has already been issued for one or more selected ordinances. Only one warning per ordinance is allowed.");
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
                        ProofUrls = [],
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
                    newWarningTicket.ControlNumber = $"WRN-{newWarningTicket.Id:D4}";
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
                        VendorType = vendor.Type,
                        BusinessId = vendor.BusinessId,
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
                        filesWithKeys[file] = GenerateFileKey(file);
                    }

                    var keys = await _storageService.UploadEvidencesAsync(filesWithKeys);

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
                    ProofUrls = [],
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
                await UpdateVendorComplianceScoreAsync(vendor.Id);
                await _unitOfWork.CommitAsync();

                if (!string.IsNullOrEmpty(vendor.Email))
                {
                    var firstName = WebUtility.HtmlEncode(vendor.FirstName);
                    var lastName = WebUtility.HtmlEncode(vendor.LastName);
                    var businessName = WebUtility.HtmlEncode(vendor.BusinessName);

                    string subject = $"Notice of Violation Ticket - Control No. {newTicket.ControlNumber}";
                    string body = $@"
                        <div style=""font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 24px; color: #333;"">
                            <h2 style=""color: #0F3D7A; margin-bottom: 4px;"">Marikina Public Market Inspection System</h2>
                            <p style=""color: #666; margin-top: 0;"">Notice of Violation Ticket</p>

                            <p>Dear {firstName} {lastName},</p>
                            <p>This is to inform you that a violation ticket has been issued for your stall, {businessName}.</p>

                            <div style=""background: #F5F5F5; border-radius: 8px; padding: 16px; margin: 20px 0;"">
                                <table style=""width: 100%; font-size: 14px; border-collapse: collapse;"">
                                    <tr><td style=""padding: 4px 0; color: #666;"">Control No.</td><td style=""padding: 4px 0; font-weight: bold; text-align: right;"">{newTicket.ControlNumber}</td></tr>
                                    <tr><td style=""padding: 4px 0; color: #666;"">Total Penalty</td><td style=""padding: 4px 0; font-weight: bold; text-align: right;"">PHP {newTicket.TotalPaymentAmount:N2}</td></tr>
                                    <tr><td style=""padding: 4px 0; color: #666;"">Due Date</td><td style=""padding: 4px 0; font-weight: bold; text-align: right;"">{newTicket.IssuedAt.AddDays(15):MMMM dd, yyyy}</td></tr>
                                </table>
                            </div>

                            <p>Please settle the amount above on or before the due date to avoid further administrative action, including additional penalties or suspension of your stall permit.</p>
                            <p>If you wish to contest this ticket, please visit the market administration office within 15 days of the issue date to file an appeal.</p>
                            <p>Thank you for your prompt attention to this matter.</p>

                            <p style=""margin-top: 24px;"">Sincerely,<br>Market Administration Office</p>
                            <p style=""color: #999; font-size: 12px; margin-top: 32px;"">This is an automated message, please do not reply.</p>
                        </div>";

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
                    VendorType = vendor.Type,
                    BusinessId = vendor.BusinessId,
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

        public async Task<PageResponse<InspectionSummaryResponse>> GetInspectionsByEnforcerIdAsync(int enforcerId, int offset, ViolationType type, string search)
        {
            offset = Math.Max(offset, 0);

            var tickets = await _ticketRepository.GetInspectionsAsync(enforcerId, offset, PAGE_SIZE, type, search);

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
                BusinessId = t.BusinessId,
                VendorType = t.VendorType,
                StallNumber = t.StallNumber,
                EnforcerId = t.EnforcerId,
                Type = t.Type,
                Status = t.Status,
                Severity = t.Severity,
                OrdinanceNames = t.Ordinances,
                IssuedAt = t.IssuedAt,
                OverdueDate = t.Type == ViolationType.Ticket
                    ? t.IssuedAt.AddDays(5)
                    : null,
                UpdatedAt = t.UpdatedAt
            }).ToList();

            return new PageResponse<InspectionSummaryResponse>
            {
                Items = ticketResponse,
                HasMore = hasMore
            };
        }

        public async Task<PageResponse<VendorInspectionHistoryResponse>> GetVendorInspectionHistoryAsync(
            int vendorId,
            int offset,
            VendorInspectionHistoryFilters filters)
        {
            offset = Math.Max(offset, 0);
            var vendor = await _vendorRepository.GetByIdAsync(vendorId);

            if (vendor is null)
                throw new RecordNotFoundException("Vendor not found.");

            var total = await _ticketRepository.GetVendorInspectionHistoryCountAsync(vendorId, filters);
            var inspections = await _ticketRepository.GetVendorInspectionHistoryAsync(
                vendorId,
                offset,
                PAGE_SIZE,
                filters);

            var hasMore = inspections.Count > PAGE_SIZE;
            if (hasMore)
                inspections.RemoveAt(inspections.Count - 1);

            return new PageResponse<VendorInspectionHistoryResponse>
            {
                Items = inspections.Select(inspection => new VendorInspectionHistoryResponse
                {
                    TicketId = inspection.TicketId,
                    ControlNumber = inspection.ControlNumber ??
                        (inspection.Type == ViolationType.Warning
                            ? $"WRN-{inspection.TicketId:D4}"
                            : null),
                    Type = inspection.Type,
                    IssuedAt = inspection.IssuedAt,
                    OrdinanceNumbers = inspection.OrdinanceNumbers,
                    EnforcerName = FormatEnforcerName(
                        inspection.EnforcerFirstName,
                        inspection.EnforcerLastName)
                }).ToList(),
                HasMore = hasMore,
                Total = total
            };
        }

        public async Task<PageResponse<AdminCommunityServiceLogResponse>> GetCommunityServiceLogsAsync(
            int offset,
            TicketStatus? status)
        {
            offset = Math.Max(offset, 0);
            var total = await _ticketRepository.GetCommunityServiceLogCountAsync(status);
            var logs = await _ticketRepository.GetCommunityServiceLogsAsync(offset, PAGE_SIZE, status);
            var hasMore = logs.Count > PAGE_SIZE;

            if (hasMore)
                logs.RemoveAt(logs.Count - 1);

            return new PageResponse<AdminCommunityServiceLogResponse>
            {
                Items = logs.Select(log => new AdminCommunityServiceLogResponse
                {
                    TicketId = log.TicketId,
                    ControlNumber = log.ControlNumber,
                    BusinessId = log.BusinessId,
                    VendorName = log.VendorName,
                    CompletedHours = log.CompletedHours,
                    TotalHours = log.TotalHours,
                    LastUpdated = log.LastUpdated,
                    ProofDocumentCount = log.ProofDocumentCount,
                    Status = log.Status
                }).ToList(),
                HasMore = hasMore,
                Total = total
            };
        }

        private static string FormatEnforcerName(string firstName, string lastName)
        {
            var firstInitial = string.IsNullOrWhiteSpace(firstName)
                ? ""
                : $"{firstName[0]}.";

            return $"Insp. {lastName}, {firstInitial}";
        }

        public async Task<PageResponse<TicketSummaryResponse>> GetTicketsByEnforcerIdAsync(
            int enforcerId,
            int offset,
            TicketStatus status,
            string search
            )
        {
            offset = Math.Max(offset, 0);

            var tickets = await _ticketRepository.GetTicketsAsync(enforcerId, offset, PAGE_SIZE, status, search);

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
                VendorType = t.VendorType,
                BusinessId = t.BusinessId,
                StallNumber = t.StallNumber,
                EnforcerId = t.EnforcerId,
                Status = t.Status ?? TicketStatus.Pending,
                IssuedAt = t.IssuedAt,
                UpdatedAt = t.UpdatedAt,
                OverdueDate = t.IssuedAt.AddDays(5)
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
            if (request.NewStatus is TicketStatus.Paid or TicketStatus.Cleared or TicketStatus.Waived)
                ticket.ResolvedAt = previousStatus == request.NewStatus && ticket.ResolvedAt.HasValue
                    ? ticket.ResolvedAt
                    : ticket.UpdatedAt;
            else
                ticket.ResolvedAt = null;

            await _notificationService.SaveNotificationAsync(
                ticket.Id,
                ticket.EnforcerId,
                $"{ticket.Vendor?.BusinessName} — status changed from {previousStatus} to {ticket.Status}.",
                ticket.Status ?? TicketStatus.Pending
            );
            await _ticketRepository.SaveChangesAsync();
            if (ticket.Type == ViolationType.Ticket)
                await UpdateVendorComplianceScoreAsync(ticket.VendorId);

            if (!string.IsNullOrEmpty(ticket?.Vendor?.User?.Email))
            {
                var firstName = WebUtility.HtmlEncode(ticket.Vendor.User.FirstName);
                var lastName = WebUtility.HtmlEncode(ticket.Vendor.User.LastName);

                string subject = $"Ticket Status Update - Control No. {ticket.ControlNumber}";
                string body = $@"
            <div style=""font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 24px; color: #333;"">
                <h2 style=""color: #0F3D7A; margin-bottom: 4px;"">Marikina Public Market Inspection System</h2>
                <p style=""color: #666; margin-top: 0;"">Ticket Status Update</p>

                <p>Dear {firstName} {lastName},</p>
                <p>This is to inform you that the status of your ticket has been updated.</p>

                <div style=""background: #F5F5F5; border-radius: 8px; padding: 16px; margin: 20px 0;"">
                    <table style=""width: 100%; font-size: 14px; border-collapse: collapse;"">
                        <tr><td style=""padding: 4px 0; color: #666;"">Control No.</td><td style=""padding: 4px 0; font-weight: bold; text-align: right;"">{ticket.ControlNumber}</td></tr>
                        <tr><td style=""padding: 4px 0; color: #666;"">Previous Status</td><td style=""padding: 4px 0; font-weight: bold; text-align: right;"">{previousStatus}</td></tr>
                        <tr><td style=""padding: 4px 0; color: #666;"">Current Status</td><td style=""padding: 4px 0; font-weight: bold; text-align: right;"">{ticket.Status}</td></tr>
                        <tr><td style=""padding: 4px 0; color: #666;"">Updated At</td><td style=""padding: 4px 0; font-weight: bold; text-align: right;"">{ticket.UpdatedAt:MMMM dd, yyyy - hh:mm tt} UTC</td></tr>
                    </table>
                </div>

                <p>Please review your ticket details for further information. If you have any questions or concerns regarding this update, please visit the market administration office.</p>
                <p>Thank you for your attention to this matter.</p>

                <p style=""margin-top: 24px;"">Sincerely,<br>Market Administration Office</p>
                <p style=""color: #999; font-size: 12px; margin-top: 32px;"">This is an automated message, please do not reply.</p>
            </div>";

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
                BusinessId = t.BusinessId,
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
                    var firstName = WebUtility.HtmlEncode(ticket.Vendor.User.FirstName);
                    var lastName = WebUtility.HtmlEncode(ticket.Vendor.User.LastName);

                    string subject = $"Overdue Notice - Control No. {ticket.ControlNumber}";
                    string body = $@"
                <div style=""font-family: Arial, sans-serif; max-width: 480px; margin: 0 auto; padding: 24px; color: #333;"">
                    <h2 style=""color: #0F3D7A; margin-bottom: 4px;"">Marikina Public Market Inspection System</h2>
                    <p style=""color: #666; margin-top: 0;"">Overdue Notice</p>

                    <p>Dear {firstName} {lastName},</p>
                    <p>This is to inform you that your ticket has now become overdue.</p>

                    <div style=""background: #F5F5F5; border-radius: 8px; padding: 16px; margin: 20px 0;"">
                        <table style=""width: 100%; font-size: 14px; border-collapse: collapse;"">
                            <tr><td style=""padding: 4px 0; color: #666;"">Control No.</td><td style=""padding: 4px 0; font-weight: bold; text-align: right;"">{ticket.ControlNumber}</td></tr>
                            <tr><td style=""padding: 4px 0; color: #666;"">Total Penalty</td><td style=""padding: 4px 0; font-weight: bold; text-align: right;"">PHP {ticket.TotalPaymentAmount:N2}</td></tr>
                            <tr><td style=""padding: 4px 0; color: #666;"">Original Due Date</td><td style=""padding: 4px 0; font-weight: bold; text-align: right;"">{ticket.IssuedAt.AddDays(15):MMMM dd, yyyy}</td></tr>
                        </table>
                    </div>

                    <p>Please settle your outstanding balance as soon as possible to avoid further administrative action, including additional penalties or suspension of your stall permit.</p>
                    <p>If you wish to contest this ticket, please visit the market administration office to file an appeal.</p>
                    <p>Thank you for your prompt attention to this matter.</p>

                    <p style=""margin-top: 24px;"">Sincerely,<br>Market Administration Office</p>
                    <p style=""color: #999; font-size: 12px; margin-top: 32px;"">This is an automated message, please do not reply.</p>
                </div>";

                    await _notificationService.SendEmailAsync(ticket.Vendor.User.Email, subject, body);
                }
            }

            if (newlyOverdueTickets.Count > 0)
            {
                await _ticketRepository.SaveChangesAsync();
                foreach (var vendorId in newlyOverdueTickets.Select(t => t.VendorId).Distinct())
                    await UpdateVendorComplianceScoreAsync(vendorId);
            }

            return newlyOverdueTickets.Count;
        }

        private async Task UpdateVendorComplianceScoreAsync(int vendorId)
        {
            var vendor = await _vendorRepository.GetVendorProfileForUpdateAsync(vendorId);
            if (vendor is null)
                throw new RecordNotFoundException("Vendor not found while updating compliance score.");

            var calculatedAt = DateTime.UtcNow;
            var tickets = await _vendorRepository.GetVendorComplianceTicketsAsync(
                vendorId,
                calculatedAt.AddDays(-365),
                calculatedAt);
            var complianceScore = _complianceScoreService.Calculate(tickets, calculatedAt);

            vendor.ComplianceScore = complianceScore.ComplianceScore;
            vendor.ScoreUpdatedAt = calculatedAt;

            await _unitOfWork.SaveChangesAsync();
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

        private static void ValidateSettlementProof(IFormFile? file)
        {
            if (file is null || file.Length <= 0)
                throw new InvalidRequestException("A settlement proof file is required.");

            const long maxFileSize = 5 * 1024 * 1024;

            if (file.Length > maxFileSize)
                throw new InvalidRequestException("Settlement proof files must not exceed 5 MB.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (extension is ".jpg" or ".jpeg"
                && string.Equals(file.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
                return;

            if (extension == ".png"
                && string.Equals(file.ContentType, "image/png", StringComparison.OrdinalIgnoreCase))
                return;

            throw new InvalidRequestException("Settlement proof must be a JPG or PNG image.");
        }
    }
}
