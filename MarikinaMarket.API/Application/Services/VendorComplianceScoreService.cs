using MarikinaMarket.API.Application.DTOs.Vendor.Internal;
using MarikinaMarket.API.Application.DTOs.Vendor.Response;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Services
{
    public class VendorComplianceScoreService : IVendorComplianceScoreService
    {
        private const int WindowDays = 365;
        private const int MaximumTickets = 5;
        private const int RecencyHalfLifeDays = 30;
        private const int MonetaryDueDays = 15;

        public VendorComplianceScoreResponse Calculate(
            IReadOnlyCollection<VendorComplianceTicket> tickets,
            DateTime calculatedAt)
        {
            var violationTickets = tickets
                .Where(t => t.IssuedAt >= calculatedAt.AddDays(-WindowDays) && t.IssuedAt <= calculatedAt)
                .ToList();

            var count = violationTickets.Count;
            var frequency = count == 0
                ? 100
                : Math.Max(0, 100.0 * (1 - (double)count / MaximumTickets));

            var latestTicketAt = violationTickets
                .Select(t => (DateTime?)t.IssuedAt)
                .Max();
            var daysSinceLastTicket = latestTicketAt.HasValue
                ? Math.Min(WindowDays, Math.Max(0, (calculatedAt - latestTicketAt.Value).Days))
                : (int?)null;
            var recency = daysSinceLastTicket.HasValue
                ? 100 * (1 - Math.Pow(2, -(double)daysSinceLastTicket.Value / RecencyHalfLifeDays))
                : 100;

            var averageSeverityPoints = count == 0
                ? (double?)null
                : violationTickets.Average(GetSeverityPoints);
            var severityScore = averageSeverityPoints.HasValue
                ? 100 * (1 - (averageSeverityPoints.Value - 1) / 3)
                : 100;

            var countedPaymentTickets = violationTickets
                .Where(ticket => IsCountedMonetaryTicket(ticket, calculatedAt))
                .ToList();
            var paymentCredits = countedPaymentTickets.Sum(t =>
            {
                if (t.Status == TicketStatus.Paid)
                    return t.ResolvedAt.HasValue &&
                        t.ResolvedAt.Value <= t.IssuedAt.AddDays(MonetaryDueDays)
                            ? 1.0
                            : 0.5;

                return 0.0;
            });
            var paymentHistory = countedPaymentTickets.Count == 0
                ? 100
                : 100 * paymentCredits / countedPaymentTickets.Count;

            var score = (0.30 * frequency)
                + (0.20 * recency)
                + (0.30 * severityScore)
                + (0.20 * paymentHistory);
            var roundedScore = (int)Math.Round(score, MidpointRounding.AwayFromZero);

            return new VendorComplianceScoreResponse
            {
                ComplianceScore = roundedScore,
                Standing = GetStanding(roundedScore),
                ViolationFrequency = Math.Round(frequency, 1),
                TicketsInWindow = count,
                ViolationFrequencyLevel = GetFrequencyLevel(count),
                Recency = Math.Round(recency, 1),
                DaysSinceLastTicket = daysSinceLastTicket,
                CategorySeverity = Math.Round(severityScore, 1),
                AverageSeverityPoints = averageSeverityPoints.HasValue
                    ? Math.Round(averageSeverityPoints.Value, 2)
                    : null,
                CategorySeverityLevel = GetSeverityLevel(averageSeverityPoints),
                PenaltyPaymentHistory = Math.Round(paymentHistory, 1),
                PaymentHistoryTickets = countedPaymentTickets.Count,
                CalculatedAt = calculatedAt
            };
        }

        private bool IsCountedMonetaryTicket(VendorComplianceTicket ticket, DateTime calculatedAt)
        {
            if (ticket.PenaltyType != PenaltyType.CashFine || !ticket.TotalPaymentAmount.HasValue)
                return false;

            if (ticket.Status == TicketStatus.Paid)
                return true;

            if (ticket.Status is not (TicketStatus.Pending or TicketStatus.Overdue or TicketStatus.Contested))
                return false;

            return ticket.IssuedAt.AddDays(MonetaryDueDays) <= calculatedAt;
        }

        private static int GetSeverityPoints(VendorComplianceTicket ticket)
        {
            return ticket.HighestSeverity switch
            {
                Severity.Moderate => 2,
                Severity.High => 3,
                _ => 1
            };
        }

        private static string GetFrequencyLevel(int ticketCount)
        {
            if (ticketCount <= 1)
                return "Low";

            if (ticketCount <= 3)
                return "Moderate";

            return "High";
        }

        private static string GetSeverityLevel(double? averageSeverityPoints)
        {
            if (!averageSeverityPoints.HasValue || averageSeverityPoints.Value < 1.67)
                return "Low";

            if (averageSeverityPoints.Value <= 2.33)
                return "Moderate";

            return "High";
        }

        private static string GetStanding(int score)
        {
            if (score >= 85)
                return "Good Standing";

            if (score >= 70)
                return "Fair Standing";

            if (score >= 50)
                return "At Risk";

            return "Poor Standing";
        }
    }
}
