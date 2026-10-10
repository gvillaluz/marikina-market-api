using System.Reflection;
using MarikinaMarket.API.Application.DTOs.Tickets.Internal;
using MarikinaMarket.API.Application.DTOs.Tickets.Request;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Enums;
using Xunit;

namespace MarikinaMarket.Tests;

public class InspectionPaginationTests
{
    [Fact]
    public async Task GetAdminInspectionAsync_ReturnsFilteredTotalWhenPageIsEmpty()
    {
        var repository = InspectionRepositoryStub.Create();
        var stub = (InspectionRepositoryStub)(object)repository;
        stub.Total = 3;
        var filters = new InspectionSummaryFilters { MarketSectionId = 4, Type = ViolationType.Warning };
        var service = CreateService(repository);

        var response = await service.GetAdminInspectionAsync(30, filters);

        Assert.Empty(response.Items);
        Assert.False(response.HasMore);
        Assert.Equal(3, response.Total);
        Assert.Same(filters, stub.FiltersUsedForCount);
        Assert.Same(filters, stub.FiltersUsedForPage);
    }

    [Fact]
    public async Task GetAdminInspectionAsync_UsesFilteredTotalForNonEmptyPage()
    {
        var repository = InspectionRepositoryStub.Create();
        var stub = (InspectionRepositoryStub)(object)repository;
        stub.Total = 12;
        stub.Items = [new AdminInspectionSummary
        {
            Id = 42,
            EnforcerId = 1,
            EnforcerFirstName = "Enforcer",
            EnforcerLastName = "One",
            VendorId = 2,
            VendorFirstName = "Vendor",
            VendorLastName = "Two",
            StallNumber = "A1",
            BusinessName = "Shop",
            MarketSectionId = 4,
            MarketSectionName = "Section 4",
            Type = ViolationType.Warning
        }];
        var filters = new InspectionSummaryFilters { MarketSectionId = 4, Search = "Shop" };
        var service = CreateService(repository);

        var response = await service.GetAdminInspectionAsync(0, filters);

        Assert.Single(response.Items);
        Assert.Equal(12, response.Total);
        Assert.Same(filters, stub.FiltersUsedForCount);
        Assert.Same(filters, stub.FiltersUsedForPage);
    }

    private static TicketService CreateService(ITicketRepository repository)
        => new(repository, null!, null!, null!, null!, null!, null!);
}

public class InspectionRepositoryStub : DispatchProxy
{
    public List<AdminInspectionSummary> Items { get; set; } = [];
    public int Total { get; set; }
    public InspectionSummaryFilters? FiltersUsedForPage { get; private set; }
    public InspectionSummaryFilters? FiltersUsedForCount { get; private set; }

    public static ITicketRepository Create()
        => DispatchProxy.Create<ITicketRepository, InspectionRepositoryStub>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod?.Name == nameof(ITicketRepository.GetAdminInspectionAsync))
        {
            FiltersUsedForPage = (InspectionSummaryFilters)args![2]!;
            return Task.FromResult(Items);
        }

        if (targetMethod?.Name == nameof(ITicketRepository.GetAdminInspectionCountAsync))
        {
            FiltersUsedForCount = (InspectionSummaryFilters)args![0]!;
            return Task.FromResult(Total);
        }

        throw new InvalidOperationException($"Unexpected repository call: {targetMethod?.Name}");
    }
}
