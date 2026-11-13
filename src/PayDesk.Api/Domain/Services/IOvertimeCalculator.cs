namespace PayDesk.Api.Domain.Services;

public interface IOvertimeCalculator
{
    OvertimeResult Calculate(IEnumerable<DailyHours> daysInPayPeriod);
}
