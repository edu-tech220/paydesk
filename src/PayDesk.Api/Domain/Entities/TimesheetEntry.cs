using PayDesk.Api.Domain.Enums;

namespace PayDesk.Api.Domain.Entities;

public class TimesheetEntry : IConcurrencyVersioned
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int PayPeriodId { get; set; }
    public PayPeriod? PayPeriod { get; set; }
    public DateOnly WorkDate { get; set; }
    public decimal HoursWorked { get; set; }
    public TimesheetEntryStatus Status { get; set; } = TimesheetEntryStatus.Submitted;
    public int? ApprovedByEmployeeId { get; set; }
    public Employee? ApprovedByEmployee { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Optimistic concurrency token, incremented manually by PayDeskDbContext on every
    /// update. Kept as a plain int (instead of SQL Server's rowversion/timestamp type)
    /// so the same model works against both SQL Server and the SQLite used in tests.
    /// </summary>
    public int Version { get; set; }
}
