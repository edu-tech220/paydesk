namespace PayDesk.Web.Models;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public int EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class EmployeeDto
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    public int? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public DateOnly HireDate { get; set; }
    public bool IsActive { get; set; }
}

public class TimesheetEntryDto
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

public class PayPeriodDto
{
    public int Id { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Version { get; set; }
}

public class PayslipDto
{
    public int Id { get; set; }
    public int PayrollRunId { get; set; }
    public int PayPeriodId { get; set; }
    public DateOnly PayPeriodStart { get; set; }
    public DateOnly PayPeriodEnd { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public decimal RegularHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal RegularPay { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal GrossPay { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetPay { get; set; }
}

public class PayrollRunDto
{
    public int Id { get; set; }
    public int PayPeriodId { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
    public decimal TotalGrossPay { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public decimal TotalNetPay { get; set; }
    public List<PayslipDto> Payslips { get; set; } = new();
}

public class ApiProblemException : Exception
{
    public int? StatusCode { get; }

    public ApiProblemException(string message, int? statusCode) : base(message)
    {
        StatusCode = statusCode;
    }
}
