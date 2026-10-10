using MarikinaMarket.API.Application.DTOs.Analytics.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Services
{
    public class DashboardService : IDashboardService
    {
        private const int RECENT_ACTIVITY_LIMIT = 5;
        private readonly ITicketRepository _ticketRepository;
        private readonly IVendorRepository _vendorRepository;
        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IUserRepository _userRepository;

        public DashboardService(
            ITicketRepository ticketRepository,
            IVendorRepository vendorRepository,
            IAuditLogRepository auditLogRepository,
            IUserRepository userRepository)
        {
            _ticketRepository = ticketRepository;
            _vendorRepository = vendorRepository;
            _auditLogRepository = auditLogRepository;
            _userRepository = userRepository;
        }

        public async Task<DashboardSummaryResponse> GetSummaryAsync()
        {
            var asOf = DateTime.UtcNow;
            var manilaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            var localToday = TimeZoneInfo.ConvertTimeFromUtc(asOf, manilaTimeZone).Date;
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localToday, DateTimeKind.Unspecified), manilaTimeZone);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(localToday.AddDays(1), DateTimeKind.Unspecified), manilaTimeZone);

            var ticketCounts = await _ticketRepository.GetDashboardTicketCountsAsync(startUtc, endUtc);
            var vendorCounts = await _vendorRepository.GetDashboardVendorCountsAsync();
            var activities = await _auditLogRepository.GetRecentDashboardActivitiesAsync(RECENT_ACTIVITY_LIMIT);

            var actorIds = activities
                .Where(activity => activity.UserId.HasValue)
                .Select(activity => activity.UserId!.Value)
                .Distinct()
                .ToArray();
            var actorNames = actorIds.Length == 0
                ? new Dictionary<int, UserNamesResponse>()
                : await _userRepository.GetNamesByIdsAsync(actorIds);

            return new DashboardSummaryResponse
            {
                AsOf = asOf,
                Metrics = new DashboardMetricsResponse
                {
                    InspectionsToday = ticketCounts.InspectionsToday,
                    OpenTickets = ticketCounts.OpenTickets,
                    ActiveVendors = vendorCounts.ActiveVendors,
                    PendingRegistrations = vendorCounts.PendingRegistrations
                },
                RecentActivity = activities.Select(activity => MapActivity(activity, actorNames)).ToList(),
                AttentionItems = BuildAttentionItems(ticketCounts.OpenTickets, vendorCounts.PendingRegistrations)
            };
        }

        private static DashboardActivityResponse MapActivity(
            AuditLog activity,
            IReadOnlyDictionary<int, UserNamesResponse> actorNames)
        {
            var category = activity.Module switch
            {
                Module.Tickets when activity.Action == "SaveNewInspection" => "Inspection",
                Module.Tickets => "Ticket",
                Module.Vendors => "VendorRegistration",
                Module.Users or Module.Security => "Account",
                _ => "Account"
            };
            var title = category == "Inspection"
                ? activity.Result == LogResult.Success ? "Inspection completed" : "Inspection failed"
                : Humanize(activity.Action);
            string? actorName = null;
            if (activity.UserId.HasValue && actorNames.TryGetValue(activity.UserId.Value, out var user))
                actorName = $"{user.FirstName} {user.LastName}";

            return new DashboardActivityResponse
            {
                Id = activity.Id,
                OccurredAt = activity.Timestamp,
                Category = category,
                Title = title,
                Description = activity.Details,
                ActorName = actorName
            };
        }

        private static List<DashboardAttentionItemResponse> BuildAttentionItems(int openTickets, int pendingRegistrations)
        {
            var items = new List<DashboardAttentionItemResponse>();
            if (openTickets > 0)
            {
                items.Add(new DashboardAttentionItemResponse
                {
                    Id = "open-tickets",
                    Type = "OpenTickets",
                    Title = "Open tickets",
                    Count = openTickets
                });
            }
            if (pendingRegistrations > 0)
            {
                items.Add(new DashboardAttentionItemResponse
                {
                    Id = "pending-registrations",
                    Type = "PendingRegistrations",
                    Title = "Pending registrations",
                    Count = pendingRegistrations
                });
            }
            return items;
        }

        private static string Humanize(string action)
        {
            if (string.IsNullOrWhiteSpace(action)) return "Activity";
            var title = new System.Text.StringBuilder(action.Length + 8);
            for (var index = 0; index < action.Length; index++)
            {
                if (index > 0 && char.IsUpper(action[index]) && char.IsLower(action[index - 1]))
                    title.Append(' ');
                title.Append(action[index]);
            }
            return title.ToString();
        }
    }
}
