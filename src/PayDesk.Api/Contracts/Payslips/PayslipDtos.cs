namespace PayDesk.Api.Contracts.Payslips;

public class PayslipResponse
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

public class PayrollRunResponse
{
    public int Id { get; set; }
    public int PayPeriodId { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
    public decimal TotalGrossPay { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public decimal TotalNetPay { get; set; }
    public IReadOnlyList<PayslipResponse> Payslips { get; set; } = Array.Empty<PayslipResponse>();
}
