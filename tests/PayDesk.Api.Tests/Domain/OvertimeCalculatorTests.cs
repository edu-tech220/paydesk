using PayDesk.Api.Domain.Services;
using Xunit;

namespace PayDesk.Api.Tests.Domain;

public class OvertimeCalculatorTests
{
    private readonly OvertimeCalculator _sut = new();

    // Monday 2026-01-05 through Sunday 2026-01-11 is a single ISO week.
    private static DateOnly Day(int offsetFromMonday) => new DateOnly(2026, 1, 5).AddDays(offsetFromMonday);

    [Fact]
    public void Calculate_WithNoDaysOver8AndWeekUnder40_ReturnsAllRegularHours()
    {
        var days = new[]
        {
            new DailyHours(Day(0), 8m),
            new DailyHours(Day(1), 8m),
            new DailyHours(Day(2), 8m),
            new DailyHours(Day(3), 8m),
            new DailyHours(Day(4), 8m),
        };

        var result = _sut.Calculate(days);

        Assert.Equal(40m, result.RegularHours);
        Assert.Equal(0m, result.OvertimeHours);
    }

    [Fact]
    public void Calculate_WithOneLongDay_SplitsThatDayIntoRegularAndOvertime()
    {
        var days = new[]
        {
            new DailyHours(Day(0), 10m),
            new DailyHours(Day(1), 8m),
            new DailyHours(Day(2), 8m),
            new DailyHours(Day(3), 8m),
            new DailyHours(Day(4), 8m),
        };

        var result = _sut.Calculate(days);

        Assert.Equal(40m, result.RegularHours);
        Assert.Equal(2m, result.OvertimeHours);
    }

    [Fact]
    public void Calculate_WithNoDayOver8ButWeeklyTotalOver40_ConvertsSpilloverToOvertime()
    {
        var days = new[]
        {
            new DailyHours(Day(0), 7m),
            new DailyHours(Day(1), 7m),
            new DailyHours(Day(2), 7m),
            new DailyHours(Day(3), 7m),
            new DailyHours(Day(4), 7m),
            new DailyHours(Day(5), 7m),
        };

        var result = _sut.Calculate(days);

        Assert.Equal(40m, result.RegularHours);
        Assert.Equal(2m, result.OvertimeHours);
    }

    [Fact]
    public void Calculate_DoesNotDoubleCountDailyOvertimeTowardsWeeklyThreshold()
    {
        // 6 days x 9h = 54h. Daily OT = 1h/day = 6h. Remaining regular = 8*6=48 > 40,
        // so 8h more becomes weekly overtime. Total OT = 6 + 8 = 14, regular = 40.
        var days = Enumerable.Range(0, 6).Select(i => new DailyHours(Day(i), 9m));

        var result = _sut.Calculate(days);

        Assert.Equal(40m, result.RegularHours);
        Assert.Equal(14m, result.OvertimeHours);
    }

    [Fact]
    public void Calculate_WithNoEntries_ReturnsZero()
    {
        var result = _sut.Calculate(Array.Empty<DailyHours>());

        Assert.Equal(0m, result.RegularHours);
        Assert.Equal(0m, result.OvertimeHours);
    }

    [Fact]
    public void Calculate_SpansTwoWeeks_AppliesWeeklyThresholdPerWeekIndependently()
    {
        // Week 1 (Mon-Sun): 5 x 8h = 40h regular, no OT.
        // Week 2 starts the following Monday: 1 day x 9h -> 8h regular + 1h OT.
        var days = new[]
        {
            new DailyHours(Day(0), 8m),
            new DailyHours(Day(1), 8m),
            new DailyHours(Day(2), 8m),
            new DailyHours(Day(3), 8m),
            new DailyHours(Day(4), 8m),
            new DailyHours(Day(7), 9m),
        };

        var result = _sut.Calculate(days);

        Assert.Equal(48m, result.RegularHours);
        Assert.Equal(1m, result.OvertimeHours);
    }
}
