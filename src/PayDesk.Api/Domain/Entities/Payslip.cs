namespace PayDesk.Api.Domain.Entities;

public class Payslip
{
    public int Id { get; set; }
    public int PayrollRunId { get; set; }
    public PayrollRun? PayrollRun { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }

    public decimal RegularHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal RegularPay { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal GrossPay { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal NetPay { get; set; }
}
