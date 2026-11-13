using PayDesk.Api.Domain.Enums;

namespace PayDesk.Api.Domain.Entities;

public class PayPeriod : IConcurrencyVersioned
{
    public int Id { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public PayPeriodStatus Status { get; set; } = PayPeriodStatus.Open;

    /// <summary>
    /// Optimistic concurrency token, incremented manually by PayDeskDbContext on every
    /// update. Guards the Open/Closed/Processed/Paid state machine against racing
    /// requests (e.g. two "process payroll" calls for the same period).
    /// </summary>
    public int Version { get; set; }

    public ICollection<TimesheetEntry> TimesheetEntries { get; set; } = new List<TimesheetEntry>();
    public PayrollRun? PayrollRun { get; set; }
}
