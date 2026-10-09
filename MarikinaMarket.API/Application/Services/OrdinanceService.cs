using MarikinaMarket.API.Application.DTOs.Ordinance.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.DTOs.SystemConfiguration.Response;
using MarikinaMarket.API.Domain.Enums;
using MarikinaMarket.API.Application.DTOs.Ordinance.Request;
using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Services
{
    public class OrdinanceService : IOrdinanceService
    {
        private readonly IOrdinanceRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public OrdinanceService(IOrdinanceRepository repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task<List<OrdinanceSummaryResponse>> GetOrdinances()
        {
            var ordinances = await _repository.GetOrdinances();

            return ordinances.Select(Summary).ToList();
        }

        public async Task<OrdinanceDetailResponse> GetByIdAsync(int id)
        {
            ValidateId(id);
            var ordinance = await _repository.GetByIdAsync(id)
                ?? throw new RecordNotFoundException("Ordinance not found.");
            return MapResponse(ordinance, new OrdinanceDetailResponse
            {
                OrdinanceNo = ordinance.OrdinanceNo,
                Series = ordinance.Series,
                MarketCode = ordinance.MarketCode,
                Title = ordinance.Title,
                Description = ordinance.Description,
                PenaltyTiers = ordinance.PenaltyTiers.OrderBy(t => t.OffenseNumber)
                    .Select(t => new OrdinancePenaltyTierResponse
                    {
                        Id = t.Id, OffenseNumber = t.OffenseNumber,
                        Severity = t.Severity, PenaltyAmount = t.PenaltyAmount
                    }).ToList()
            });
        }

        public async Task<OrdinanceSummaryResponse> CreateAsync(SaveOrdinanceRequest request)
        {
            var ordinanceNo = ValidateRequest(request);
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                await EnsureUniqueNumberAsync(ordinanceNo);
                var ordinance = new Ordinance
                {
                    OrdinanceNo = ordinanceNo,
                    Series = request.Series.Trim(),
                    MarketCode = request.MarketCode.Trim(),
                    Title = request.Title.Trim(),
                    Description = request.Description.Trim(),
                    Category = request.Category,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                await _repository.AddAsync(ordinance);
                await _unitOfWork.SaveChangesAsync();
                var tiers = request.PenaltyTiers.Select(t => CreateTier(ordinance.Id, t)).ToList();
                ordinance.PenaltyTiers = tiers;
                await _repository.AddTiersAsync(tiers);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return Summary(ordinance);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public async Task<OrdinanceSummaryResponse> UpdateAsync(int id, SaveOrdinanceRequest request)
        {
            ValidateId(id);
            var ordinanceNo = ValidateRequest(request);
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var ordinance = await _repository.GetByIdAsync(id)
                    ?? throw new RecordNotFoundException("Ordinance not found.");
                await EnsureUniqueNumberAsync(ordinanceNo, id);

                if (ordinance.OrdinanceNo != ordinanceNo) ordinance.OrdinanceNo = ordinanceNo;
                if (ordinance.Series != request.Series.Trim()) ordinance.Series = request.Series.Trim();
                if (ordinance.MarketCode != request.MarketCode.Trim()) ordinance.MarketCode = request.MarketCode.Trim();
                if (ordinance.Title != request.Title.Trim()) ordinance.Title = request.Title.Trim();
                if (ordinance.Description != request.Description.Trim()) ordinance.Description = request.Description.Trim();
                if (ordinance.Category != request.Category) ordinance.Category = request.Category;

                await ReconcileTiersAsync(ordinance, request.PenaltyTiers);

                ordinance.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                return Summary(ordinance);
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        private async Task ReconcileTiersAsync(Ordinance ordinance, List<SavePenaltyTierRequest> requests)
        {
            var requestedNumbers = requests.Select(t => Math.Max(1, t.OffenseNumber)).ToHashSet();
            var removed = ordinance.PenaltyTiers.Where(t => !requestedNumbers.Contains(t.OffenseNumber)).ToList();
            _repository.RemoveTiers(removed);
            foreach (var tier in removed) ordinance.PenaltyTiers.Remove(tier);

            var existing = ordinance.PenaltyTiers.ToDictionary(t => t.OffenseNumber);
            var added = new List<OrdinancePenaltyTier>();
            foreach (var request in requests)
            {
                var number = Math.Max(1, request.OffenseNumber);
                if (existing.TryGetValue(number, out var tier))
                {
                    if (tier.Severity != request.Severity) tier.Severity = request.Severity;
                    if (tier.PenaltyAmount != request.PenaltyAmount) tier.PenaltyAmount = request.PenaltyAmount;
                }
                else
                {
                    var newTier = CreateTier(ordinance.Id, request);
                    ordinance.PenaltyTiers.Add(newTier);
                    added.Add(newTier);
                }
            }
            if (added.Count > 0) await _repository.AddTiersAsync(added);
        }

        private async Task EnsureUniqueNumberAsync(string number, int? excludingId = null)
        {
            if (await _repository.NumberExistsAsync(number, excludingId))
                throw new InvalidRequestException("An ordinance with this number already exists.");
        }

        private static void ValidateId(int id)
        {
            if (id <= 0) throw new InvalidRequestException("Ordinance ID must be greater than zero.");
        }

        private static string ValidateRequest(SaveOrdinanceRequest request)
        {
            if (request is null) throw new InvalidRequestException("Ordinance data is required.");
            ValidateText(request.OrdinanceNumber, "Ordinance number", 100);
            ValidateText(request.Series, "Series", 100);
            ValidateText(request.MarketCode, "Market code");
            ValidateText(request.Title, "Title", 250);
            ValidateText(request.Description, "Description", 1000);
            var number = $"Ord. No. {request.OrdinanceNumber.Trim()} Series of {request.Series.Trim()}";
            if (number.Length > 100)
                throw new InvalidRequestException("Formatted ordinance number must not exceed 100 characters.");
            if (!Enum.IsDefined(request.Category))
                throw new InvalidRequestException("Invalid ordinance category.");
            if (request.PenaltyTiers is null || request.PenaltyTiers.Count == 0)
                throw new InvalidRequestException("At least one penalty tier is required.");

            var numbers = new HashSet<int>();
            foreach (var tier in request.PenaltyTiers)
            {
                if (tier is null) throw new InvalidRequestException("Penalty tiers cannot contain null entries.");
                if (tier.OffenseNumber < 0)
                    throw new InvalidRequestException("Offense number cannot be negative.");
                if (!numbers.Add(Math.Max(1, tier.OffenseNumber)))
                    throw new InvalidRequestException("Offense numbers must be unique; base and first offense both use 1.");
                if (!Enum.IsDefined(tier.Severity))
                    throw new InvalidRequestException("Invalid penalty tier severity.");
                if (tier.PenaltyAmount < 0 || tier.PenaltyAmount > 9999999999999999.99m
                    || decimal.Round(tier.PenaltyAmount, 2) != tier.PenaltyAmount)
                    throw new InvalidRequestException("Penalty amount must be nonnegative, at most 9999999999999999.99, and have at most two decimal places.");
            }
            return number;
        }

        private static void ValidateText(string? value, string field, int? maxLength = null)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidRequestException($"{field} is required.");
            if (maxLength.HasValue && value.Trim().Length > maxLength.Value)
                throw new InvalidRequestException($"{field} must not exceed {maxLength.Value} characters.");
        }

        private static OrdinancePenaltyTier CreateTier(int ordinanceId, SavePenaltyTierRequest request) => new()
        {
            OrdinanceId = ordinanceId,
            OffenseNumber = Math.Max(1, request.OffenseNumber),
            Severity = request.Severity,
            PenaltyAmount = request.PenaltyAmount
        };

        private static OrdinanceSummaryResponse Summary(Ordinance ordinance) =>
            MapResponse(ordinance, new OrdinanceSummaryResponse
            {
                OrdinanceNo = ordinance.OrdinanceNo, 
                Series = ordinance.Series,
                MarketCode = ordinance.MarketCode, 
                Title = ordinance.Title, 
                Description = ordinance.Description
            });

        private static T MapResponse<T>(Ordinance ordinance, T response) where T : OrdinanceSummaryResponse
        {
            response.Id = ordinance.Id;
            response.Category = ordinance.Category;
            response.CreatedAt = ordinance.CreatedAt;
            response.UpdatedAt = ordinance.UpdatedAt;
            response.IsActive = ordinance.IsActive;
            response.Severity = ordinance.PenaltyTiers.FirstOrDefault(t => t.OffenseNumber == 1)?.Severity
                ?? ordinance.PenaltyTiers.OrderBy(t => t.OffenseNumber).FirstOrDefault()?.Severity;
            response.PenaltyTierCount = ordinance.PenaltyTiers.Count;
            return response;
        }

        public async Task<CountResponse> GetActiveCountAsync()
        {
            return new CountResponse
            {
                Count = await _repository.GetActiveCountAsync()
            };
        }
    }
}
