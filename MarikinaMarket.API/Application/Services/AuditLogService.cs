using System.ComponentModel.DataAnnotations;
using MarikinaMarket.API.Application.DTOs.Audits.Internal;
using MarikinaMarket.API.Application.DTOs.Audits.Request;
using MarikinaMarket.API.Application.DTOs.Audits.Response;
using MarikinaMarket.API.Application.DTOs.Tickets.Response;
using MarikinaMarket.API.Application.DTOs.User.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;

namespace MarikinaMarket.API.Application.Services
{
    public class AuditLogService : IAuditLogService
    {
        private const int PAGE_SIZE = 10;
        private readonly IAuditLogRepository _repository;
        private readonly IUserRepository _userRepository;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly AuditLogContext _audit;
        private readonly ILogger<AuditLogService> _logger;

        public AuditLogService(IAuditLogRepository repository, IServiceScopeFactory scopeFactory,
            AuditLogContext audit, ILogger<AuditLogService> logger, IUserRepository userRepository)
        {
            _repository = repository;
            _userRepository = userRepository;
            _scopeFactory = scopeFactory;
            _audit = audit;
            _logger = logger;
        }

        public async Task<PageResponse<AuditLogSummaryResponse>> GetSummariesAsync(int offset,
            AuditLogFilter filters, CancellationToken cancellationToken = default)
        {
            if (offset < 0) throw new ValidationException("Offset must be zero or greater.");
            var (fromUtc, toUtc) = GetDateRange(filters);
            var logs = await _repository.GetSummariesAsync(offset, PAGE_SIZE, filters, fromUtc, toUtc, cancellationToken);
            var total = await _repository.GetCountAsync(filters, fromUtc, toUtc, cancellationToken);
            var hasMore = logs.Count > PAGE_SIZE;
            if (hasMore) logs.RemoveAt(logs.Count - 1);
            var userIds = logs.Select(log => log.UserId).OfType<int>().Distinct().ToList();
            var names = userIds.Count > 0
                ? await _userRepository.GetNamesByIdsAsync(userIds, cancellationToken)
                : new Dictionary<int, UserNamesResponse>();
            return new PageResponse<AuditLogSummaryResponse>
            {
                Items = logs.Select(log => Summary(log,
                    log.UserId.HasValue ? names.GetValueOrDefault(log.UserId.Value) : null)).ToList(),
                HasMore = hasMore,
                Total = total
            };
        }

        public async Task<AuditLogDetailResponse> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
        {
            if (id <= 0) throw new ValidationException("Audit log ID must be greater than zero.");
            var log = await _repository.GetByIdAsync(id, cancellationToken)
                ?? throw new RecordNotFoundException("Audit log not found.");
            UserNamesResponse? user = null;
            if (log.UserId.HasValue)
            {
                var names = await _userRepository.GetNamesByIdsAsync([log.UserId.Value], cancellationToken);
                user = names.GetValueOrDefault(log.UserId.Value);
            }
            return new AuditLogDetailResponse
            {
                Id = log.Id,
                Timestamp = log.Timestamp,
                UserId = log.UserId,
                Role = log.Role,
                FirstName = user?.FirstName,
                LastName = user?.LastName,
                Action = log.Action,
                Module = log.Module,
                Result = log.Result,
                TargetId = log.TargetId,
                Details = log.Details
            };
        }

        public Task<AuditLogCountsResponse> GetActivityCountsAsync(AuditLogFilter filters,
            CancellationToken cancellationToken = default)
        {
            var (fromUtc, toUtc) = GetDateRange(filters);
            return _repository.GetActivityCountsAsync(filters, fromUtc, toUtc, cancellationToken);
        }

        private static (DateTime? FromUtc, DateTime? ToUtc) GetDateRange(AuditLogFilter filters)
        {
            Validator.ValidateObject(filters, new ValidationContext(filters), true);
            if (filters.Modules is null || filters.Results is null ||
                filters.Modules.Any(module => !Enum.IsDefined(module)) ||
                filters.Results.Any(result => !Enum.IsDefined(result)))
                throw new ValidationException("Module and result selections must contain valid options.");
            if (filters.Module.HasValue && filters.Modules.Length > 0)
                throw new ValidationException("Use either module or modules, not both.");
            if (filters.Result.HasValue && filters.Results.Length > 0)
                throw new ValidationException("Use either result or results, not both.");
            if (filters.DateRange.HasValue && (filters.FromDate.HasValue || filters.ToDate.HasValue))
                throw new ValidationException("Date range presets cannot be combined with fromDate or toDate.");

            filters.Modules = filters.Modules.Distinct().ToArray();
            filters.Results = filters.Results.Distinct().ToArray();
            if (filters.DateRange.HasValue)
            {
                var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Asia/Manila"));
                var monthStart = new DateOnly(today.Year, today.Month, 1);
                (DateOnly? from, DateOnly? toExclusive) = filters.DateRange.Value switch
                {
                    AuditLogDateRange.AllTime => ((DateOnly?)null, (DateOnly?)null),
                    AuditLogDateRange.Today => (today, today.AddDays(1)),
                    AuditLogDateRange.Yesterday => (today.AddDays(-1), today),
                    AuditLogDateRange.Last7Days => (today.AddDays(-6), today.AddDays(1)),
                    AuditLogDateRange.Last30Days => (today.AddDays(-29), today.AddDays(1)),
                    AuditLogDateRange.ThisMonth => (monthStart, today.AddDays(1)),
                    AuditLogDateRange.LastMonth => (monthStart.AddMonths(-1), monthStart),
                    _ => throw new ValidationException("Select a valid date range.")
                };
                return (ToUtc(from), ToUtc(toExclusive));
            }
            if (filters.FromDate > filters.ToDate)
                throw new ValidationException("From date must not be after to date.");
            if (filters.FromDate == DateOnly.MinValue || filters.ToDate == DateOnly.MaxValue)
                throw new ValidationException("Date filter is outside the supported range.");
            return (ToUtc(filters.FromDate), ToUtc(filters.ToDate?.AddDays(1)));
        }

        // A fresh scope prevents a failed request's tracked changes from being saved again.
        public async Task RecordAsync(AuditLog log)
        {
            try
            {
                ValidateLog(log);
                using var scope = _scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IAuditLogRepository>().AddAsync(Copy(log));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Audit persistence failed ({ExceptionType}).", ex.GetType().Name);
            }
        }

        public async Task StageCurrentAsync()
        {
            if (!_audit.SaveWithTransaction || _audit.Entry is null || _audit.Staged) return;
            try
            {
                ValidateLog(_audit.Entry);
                await _repository.AddAsync(Copy(_audit.Entry));
                _audit.Staged = true;
            }
            catch (Exception ex)
            {
                // The repository rolls back its savepoint; the business transaction remains valid.
                _logger.LogWarning("Transactional audit persistence failed ({ExceptionType}).", ex.GetType().Name);
            }
        }

        private static void ValidateLog(AuditLog log)
        {
            if (log.UserId <= 0 || !Enum.IsDefined(log.Module) || !Enum.IsDefined(log.Result) ||
                (log.Role.HasValue && !Enum.IsDefined(log.Role.Value)) ||
                string.IsNullOrWhiteSpace(log.Action) || log.Action.Length > 100 ||
                string.IsNullOrWhiteSpace(log.Details) || log.Details.Length > 500 || log.TargetId?.Length > 100)
                throw new ValidationException("Invalid audit log data.");
        }

        public async Task AddInTransactionAsync(AuditLog log)
        {
            try
            {
                ValidateLog(log);
                await _repository.AddAsync(Copy(log));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Transactional audit persistence failed ({ExceptionType}).", ex.GetType().Name);
            }
        }

        private static AuditLog Copy(AuditLog log) => new()
        {
            Timestamp = DateTime.UtcNow,
            UserId = log.UserId,
            Role = log.Role,
            Action = log.Action,
            Module = log.Module,
            TargetId = log.TargetId,
            Result = log.Result,
            Details = log.Details
        };

        private static DateTime? ToUtc(DateOnly? date) => date.HasValue
            ? TimeZoneInfo.ConvertTimeToUtc(date.Value.ToDateTime(TimeOnly.MinValue),
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila")) : null;

        private static AuditLogSummaryResponse Summary(AuditLog log, UserNamesResponse? user) => new()
        {
            Id = log.Id,
            Timestamp = log.Timestamp,
            UserId = log.UserId,
            Role = log.Role,
            FirstName = user?.FirstName,
            LastName = user?.LastName,
            Action = log.Action,
            Module = log.Module,
            Result = log.Result
        };
    }
}
