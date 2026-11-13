using System.ComponentModel.DataAnnotations;

namespace PayDesk.Api.Contracts.TimesheetEntries;

public class TimesheetEntryResponse
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public int PayPeriodId { get; set; }
    public DateOnly WorkDate { get; set; }
    public decimal HoursWorked { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public int Version { get; set; }
}

public class CreateTimesheetEntryRequest
{
    [Required]
    public int PayPeriodId { get; set; }

    [Required]
    public DateOnly WorkDate { get; set; }

    [Range(0.25, 24)]
    public decimal HoursWorked { get; set; }
}

public class ApproveTimesheetEntryRequest
{
    [Required]
    public int ExpectedVersion { get; set; }
}

public class RejectTimesheetEntryRequest
{
    [Required]
    public int ExpectedVersion { get; set; }

    [Required, MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
