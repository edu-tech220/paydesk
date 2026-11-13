using PayDesk.Api.Domain.Entities;
using PayDesk.Api.Domain.Services;
using Xunit;

namespace PayDesk.Api.Tests.Domain;

public class ProgressiveTaxCalculatorTests
{
    private readonly ProgressiveTaxCalculator _sut = new();

    private static readonly TaxBracket[] Brackets =
    {
        new() { MinAmount = 0m, MaxAmount = 1000m, Rate = 0.10m },
        new() { MinAmount = 1000m, MaxAmount = 3000m, Rate = 0.20m },
        new() { MinAmount = 3000m, MaxAmount = null, Rate = 0.30m },
    };

    [Fact]
    public void CalculateTax_WithinFirstBracket_UsesOnlyFirstRate()
    {
        var tax = _sut.CalculateTax(500m, Brackets);

        Assert.Equal(50m, tax);
    }

    [Fact]
    public void CalculateTax_SpanningTwoBrackets_TaxesEachSliceAtItsOwnRate()
    {
        // First 1000 @ 10% = 100, next 500 (1000..1500) @ 20% = 100 -> 200 total.
        var tax = _sut.CalculateTax(1500m, Brackets);

        Assert.Equal(200m, tax);
    }

    [Fact]
    public void CalculateTax_IntoTopUncappedBracket_TaxesRemainderAtTopRate()
    {
        // 1000 @ 10% = 100, 2000 @ 20% = 400, 1000 (3000..4000) @ 30% = 300 -> 800.
        var tax = _sut.CalculateTax(4000m, Brackets);

        Assert.Equal(800m, tax);
    }

    [Fact]
    public void CalculateTax_WithZeroOrNegativeGross_ReturnsZero()
    {
        Assert.Equal(0m, _sut.CalculateTax(0m, Brackets));
        Assert.Equal(0m, _sut.CalculateTax(-50m, Brackets));
    }

    [Fact]
    public void CalculateTax_IsProgressiveNotFlatOnWholeAmount()
    {
        // A flat 20% on 1500 would be 300; progressive must be lower (200).
        var tax = _sut.CalculateTax(1500m, Brackets);

        Assert.True(tax < 1500m * 0.20m);
    }
}
