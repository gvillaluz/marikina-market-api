using MarikinaMarket.API.Application.Interfaces.Services;
using MarikinaMarket.API.Application.DTOs.MarketSection.Response;
using MarikinaMarket.API.Application.DTOs.SystemConfiguration.Response;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application;
using MarikinaMarket.API.Application.DTOs.MarketSection.Request;
using MarikinaMarket.API.Domain.Entities;

namespace MarikinaMarket.API.Application.Services
{
    public class MarketSectionService : IMarketSectionService
    {
        private readonly IMarketSectionRepository _repository;

        public MarketSectionService(IMarketSectionRepository repository) => _repository = repository;

        public async Task<List<MarketSectionResponse>> GetAllAsync()
        {
            var sections = await _repository.GetAllAsync();

            return sections.Select(section => MapResponse(section, section.VendorProfiles.Count)).ToList();
        }

        public async Task<MarketSectionResponse> CreateAsync(SaveMarketSectionRequest request)
        {
            ValidateRequest(request);
            var name = request.Name.Trim();
            if (await _repository.NameExistsAsync(name))
                throw new InvalidRequestException("A market section with this name already exists.");

            var section = new MarketSection
            {
                Name = name,
                Description = request.Description.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            await _repository.AddAsync(section);
            await _repository.SaveChangesAsync();
            return MapResponse(section, 0);
        }

        public async Task<MarketSectionResponse> UpdateAsync(int id, SaveMarketSectionRequest request)
        {
            if (id <= 0)
                throw new InvalidRequestException("Market section ID must be greater than zero.");
            ValidateRequest(request);
            var section = await _repository.GetById(id)
                ?? throw new RecordNotFoundException("Market section not found.");
            var name = request.Name.Trim();
            if (await _repository.NameExistsAsync(name, id))
                throw new InvalidRequestException("A market section with this name already exists.");

            var description = request.Description.Trim();
            if (section.Name != name) section.Name = name;
            if (section.Description != description) section.Description = description;
            await _repository.SaveChangesAsync();
            return MapResponse(section, await _repository.GetVendorCountAsync(id));
        }

        private static void ValidateRequest(SaveMarketSectionRequest request)
        {
            if (request is null)
                throw new InvalidRequestException("Market section data is required.");
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 150)
                throw new InvalidRequestException("Name is required and must not exceed 150 characters.");
            if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 500)
                throw new InvalidRequestException("Description is required and must not exceed 500 characters.");
        }

        private static MarketSectionResponse MapResponse(MarketSection section, int vendorCount) => new()
        {
            Id = section.Id,
            Name = section.Name,
            Description = section.Description,
            IsActive = section.IsActive,
            VendorCount = vendorCount
        };

        public async Task<CountResponse> GetActiveCountAsync()
        {
            return new CountResponse
            {
                Count = await _repository.GetActiveCountAsync()
            };
        }

        public async Task UpdateActiveStatusAsync(int marketSectionId, bool isActive)
        {
            if (marketSectionId <= 0)
                throw new InvalidRequestException("Market section ID must be greater than zero.");

            var section = await _repository.GetById(marketSectionId);

            if (section is null)
                throw new RecordNotFoundException("Market section not found.");

            section.IsActive = isActive;
            await _repository.SaveChangesAsync();
        }
    }
}
