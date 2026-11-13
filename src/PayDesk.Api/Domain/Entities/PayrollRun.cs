namespace PayDesk.Api.Domain.Entities;

public class PayrollRun
{
    public int Id { get; set; }
    public int PayPeriodId { get; set; }
    public PayPeriod? PayPeriod { get; set; }
    public DateTime ProcessedAtUtc { get; set; }
    public decimal TotalGrossPay { get; set; }
    public decimal TotalTaxAmount { get; set; }
    public decimal TotalNetPay { get; set; }

    public ICollection<Payslip> Payslips { get; set; } = new List<Payslip>();
}
