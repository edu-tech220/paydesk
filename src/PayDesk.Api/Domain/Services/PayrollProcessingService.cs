using Microsoft.EntityFrameworkCore;
using PayDesk.Api.Data;
using PayDesk.Api.Domain.Entities;
using PayDesk.Api.Domain.Enums;
using PayDesk.Api.Domain.Exceptions;

namespace PayDesk.Api.Domain.Services;

public class PayrollProcessingService : IPayrollProcessingService
{
    private readonly PayDeskDbContext _db;
    private readonly IOvertimeCalculator _overtimeCalculator;
    private readonly IProgressiveTaxCalculator _taxCalculator;

    public PayrollProcessingService(
        PayDeskDbContext db,
        IOvertimeCalculator overtimeCalculator,
        IProgressiveTaxCalculator taxCalculator)
    {
        _db = db;
        _overtimeCalculator = overtimeCalculator;
        _taxCalculator = taxCalculator;
    }

    public async Task<PayrollRun> ProcessAsync(int payPeriodId, CancellationToken cancellationToken = default)
    {
        var payPeriod = await _db.PayPeriods
            .Include(p => p.PayrollRun)
            .ThenInclude(r => r!.Payslips)
            .FirstOrDefaultAsync(p => p.Id == payPeriodId, cancellationToken)
            ?? throw new EntityNotFoundException($"Pay period {payPeriodId} was not found.");

        if (payPeriod.PayrollRun is not null)
        {
            return payPeriod.PayrollRun;
        }

        if (payPeriod.Status != PayPeriodStatus.Closed)
        {
            throw new BusinessRuleException(
                $"Pay period {payPeriodId} must be Closed before it can be processed (current status: {payPeriod.Status}).");
        }

        var entries = await _db.TimesheetEntries
            .Include(t => t.Employee)
            .Where(t => t.PayPeriodId == payPeriodId)
            .ToListAsync(cancellationToken);

        if (entries.Any(e => e.Status == TimesheetEntryStatus.Submitted))
        {
            throw new BusinessRuleException(
                "All timesheet entries for this period must be approved or rejected before processing payroll.");
        }

        var taxBrackets = await _db.TaxBrackets.AsNoTracking().ToListAsync(cancellationToken);

        var run = new PayrollRun
        {
            PayPeriodId = payPeriodId,
            ProcessedAtUtc = DateTime.UtcNow
        };

        foreach (var group in entries.Where(e => e.Status == TimesheetEntryStatus.Approved).GroupBy(e => e.EmployeeId))
        {
            var employee = group.First().Employee!;
            var days = group.Select(e => new DailyHours(e.WorkDate, e.HoursWorked));
            var overtime = _overtimeCalculator.Calculate(days);

            var regularPay = Math.Round(overtime.RegularHours * employee.HourlyRate, 2, MidpointRounding.AwayFromZero);
            var overtimePay = Math.Round(
                overtime.OvertimeHours * employee.HourlyRate * OvertimeCalculator.OvertimeMultiplier,
                2,
                MidpointRounding.AwayFromZero);
            var grossPay = regularPay + overtimePay;
            var taxAmount = _taxCalculator.CalculateTax(grossPay, taxBrackets);

            run.Payslips.Add(new Payslip
            {
                EmployeeId = employee.Id,
                RegularHours = overtime.RegularHours,
                OvertimeHours = overtime.OvertimeHours,
                RegularPay = regularPay,
                OvertimePay = overtimePay,
                GrossPay = grossPay,
                TaxAmount = taxAmount,
                NetPay = grossPay - taxAmount
            });
        }

        run.TotalGrossPay = run.Payslips.Sum(p => p.GrossPay);
        run.TotalTaxAmount = run.Payslips.Sum(p => p.TaxAmount);
        run.TotalNetPay = run.Payslips.Sum(p => p.NetPay);

        payPeriod.Status = PayPeriodStatus.Processed;
        _db.PayrollRuns.Add(run);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request may have processed this period first (unique index on
            // PayPeriodId); treat this call as idempotent and hand back the run that
            // won the race, or rethrow if the failure was unrelated.
            _db.ChangeTracker.Clear();
            var winner = await _db.PayrollRuns
                .Include(r => r.Payslips)
                .FirstOrDefaultAsync(r => r.PayPeriodId == payPeriodId, cancellationToken);

            if (winner is null)
            {
                throw;
            }

            return winner;
        }

        return run;
    }
}
