using System.Reflection;
using MarikinaMarket.API.Application;
using MarikinaMarket.API.Application.DTOs.Ordinance.Request;
using MarikinaMarket.API.Application.Interfaces.Repositories;
using MarikinaMarket.API.Application.Services;
using MarikinaMarket.API.Domain.Entities;
using MarikinaMarket.API.Domain.Enums;
using Xunit;

namespace MarikinaMarket.Tests;

public class OrdinanceNumberTests
{
    [Fact]
    public async Task CreateAndUpdate_PreserveNumberAndLeadingZerosWithoutDisplayText()
    {
        var service = CreateService(out var boundary);
        var response = await service.CreateAsync(Request(" 011 "));
        Assert.Equal("011", response.OrdinanceNo);
        Assert.Equal("011", boundary.Ordinance!.OrdinanceNo);
        Assert.Equal("2014", response.Series);
        response = await service.UpdateAsync(1, Request("04"));
        Assert.Equal("04", response.OrdinanceNo);
        Assert.Equal("04", boundary.Ordinance.OrdinanceNo);
    }

    [Theory]
    [InlineData("Ord. No. 011")]
    [InlineData("011 Series of 2014")]
    [InlineData("-11")]
    [InlineData("11.5")]
    [InlineData("١١")]
    public async Task Save_RejectsNumbersContainingNonAsciiDigits(string number)
    {
        var service = CreateService(out var boundary);
        await Assert.ThrowsAsync<InvalidRequestException>(() => service.CreateAsync(Request(number)));
        Assert.Null(boundary.Ordinance);
    }

    private static SaveOrdinanceRequest Request(string number) => new()
    {
        OrdinanceNumber = number, Series = "2014", MarketCode = "MKT-001",
        Title = "Market Identification Card", Category = ViolationCategory.Licensing,
        PenaltyTiers = [new() { OffenseNumber = 1, Severity = Severity.Minor, PenaltyAmount = 500m }]
    };

    private static OrdinanceService CreateService(out OrdinanceNumberBoundary boundary)
    {
        var repository = DispatchProxy.Create<IOrdinanceRepository, OrdinanceNumberBoundary>();
        boundary = (OrdinanceNumberBoundary)(object)repository;
        var unitOfWork = DispatchProxy.Create<IUnitOfWork, OrdinanceNumberBoundary>();
        return new OrdinanceService(repository, unitOfWork);
    }
}

public class OrdinanceNumberBoundary : DispatchProxy
{
    public Ordinance? Ordinance { get; private set; }

    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        switch (method!.Name)
        {
            case "AddAsync":
                Ordinance = (Ordinance)args![0]!;
                Ordinance.Id = 1;
                return Task.CompletedTask;
            case "GetByIdAsync": return Task.FromResult(Ordinance);
            case "SaveChangesAsync": return Task.FromResult(1);
            case "AddTiersAsync":
            case "BeginTransactionAsync":
            case "CommitAsync":
            case "RollbackAsync": return Task.CompletedTask;
            case "RemoveTiers": return null;
            case "Dispose": return null;
            default: throw new NotSupportedException(method.Name);
        }
    }
}
