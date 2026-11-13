namespace PayDesk.Api.Domain.Services;

public record DailyHours(DateOnly WorkDate, decimal Hours);

public record OvertimeResult(decimal RegularHours, decimal OvertimeHours);
