using System.Globalization;

namespace PayDesk.Api.Domain.Services;

/// <summary>
/// Daily-plus-weekly overtime rule: any hours past 8 on a single day are overtime,
/// and on top of that any regular hours left over once a Monday-Sunday (ISO) week
/// passes 40 total are converted to overtime too. The two rules are combined
/// without double counting - hours already classified as daily overtime are not
/// considered again for the weekly threshold.
/// </summary>
public class OvertimeCalculator : IOvertimeCalculator
{
    public const decimal DailyThresholdHours = 8m;
    public const decimal WeeklyThresholdHours = 40m;
    public const decimal OvertimeMultiplier = 1.5m;

    public OvertimeResult Calculate(IEnumerable<DailyHours> daysInPayPeriod)
    {
        decimal totalRegular = 0m;
        decimal totalOvertime = 0m;

        var weeks = daysInPayPeriod.GroupBy(d => GetIsoWeekKey(d.WorkDate));

        foreach (var week in weeks)
        {
            decimal weekRegular = 0m;
            decimal weekOvertime = 0m;

            foreach (var day in week)
            {
                var dailyRegular = Math.Min(day.Hours, DailyThresholdHours);
                var dailyOvertime = Math.Max(day.Hours - DailyThresholdHours, 0m);
                weekRegular += dailyRegular;
                weekOvertime += dailyOvertime;
            }

            if (weekRegular > WeeklyThresholdHours)
            {
                var spillover = weekRegular - WeeklyThresholdHours;
                weekRegular = WeeklyThresholdHours;
                weekOvertime += spillover;
            }

            totalRegular += weekRegular;
            totalOvertime += weekOvertime;
        }

        return new OvertimeResult(totalRegular, totalOvertime);
    }

    private static (int Year, int Week) GetIsoWeekKey(DateOnly date)
    {
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        var week = ISOWeek.GetWeekOfYear(dateTime);
        var year = ISOWeek.GetYear(dateTime);
        return (year, week);
    }
}
