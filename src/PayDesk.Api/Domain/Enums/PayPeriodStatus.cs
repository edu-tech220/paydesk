namespace PayDesk.Api.Domain.Enums;

/// <summary>
/// Open: timesheets can be submitted/edited.
/// Closed: no more timesheet changes; ready for payroll processing.
/// Processed: a payroll run has produced payslips for this period.
/// Paid: the processed run has been marked as disbursed.
/// </summary>
public enum PayPeriodStatus
{
    Open = 0,
    Closed = 1,
    Processed = 2,
    Paid = 3
}
