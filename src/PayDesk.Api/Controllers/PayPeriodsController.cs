using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayDesk.Api.Contracts.Common;
using PayDesk.Api.Contracts.PayPeriods;
using PayDesk.Api.Contracts.Payslips;
using PayDesk.Api.Data;
using PayDesk.Api.Domain.Entities;
using PayDesk.Api.Domain.Enums;
using PayDesk.Api.Domain.Exceptions;
using PayDesk.Api.Domain.Services;

namespace PayDesk.Api.Controllers;

[ApiController]
[Route("api/pay-periods")]
[Authorize]
public class PayPeriodsController : ControllerBase
{
    private readonly PayDeskDbContext _db;
    private readonly IPayrollProcessingService _payrollProcessingService;

    public PayPeriodsController(PayDeskDbContext db, IPayrollProcessingService payrollProcessingService)
    {
        _db = db;
        _payrollProcessingService = payrollProcessingService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PayPeriodResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PayPeriodResponse>>> GetPayPeriods([FromQuery] PageQuery query)
    {
        var totalCount = await _db.PayPeriods.CountAsync();

        var items = await _db.PayPeriods
            .OrderByDescending(p => p.StartDate)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => ToResponse(p))
            .ToListAsync();

        return Ok(new PagedResult<PayPeriodResponse>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PayPeriodResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayPeriodResponse>> GetPayPeriod(int id)
    {
        var payPeriod = await _db.PayPeriods.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new EntityNotFoundException($"Pay period {id} was not found.");

        return Ok(ToResponse(payPeriod));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PayPeriodResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<PayPeriodResponse>> CreatePayPeriod(CreatePayPeriodRequest request)
    {
        if (request.EndDate <= request.StartDate)
        {
            return Problem(detail: "EndDate must be after StartDate.", statusCode: StatusCodes.Status400BadRequest);
        }

        var overlaps = await _db.PayPeriods.AnyAsync(p =>
            request.StartDate <= p.EndDate && request.EndDate >= p.StartDate);

        if (overlaps)
        {
            throw new BusinessRuleException("The requested date range overlaps with an existing pay period.");
        }

        var payPeriod = new PayPeriod
        {
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = PayPeriodStatus.Open
        };

        _db.PayPeriods.Add(payPeriod);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPayPeriod), new { id = payPeriod.Id }, ToResponse(payPeriod));
    }

    [HttpPost("{id:int}/close")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PayPeriodResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayPeriodResponse>> ClosePayPeriod(int id, VersionedActionRequest request)
    {
        var payPeriod = await _db.PayPeriods.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new EntityNotFoundException($"Pay period {id} was not found.");

        EnsureCurrentVersion(payPeriod, request.ExpectedVersion);

        if (payPeriod.Status != PayPeriodStatus.Open)
        {
            throw new BusinessRuleException($"Pay period {id} must be Open to be closed (current status: {payPeriod.Status}).");
        }

        var hasPendingApprovals = await _db.TimesheetEntries
            .AnyAsync(t => t.PayPeriodId == id && t.Status == TimesheetEntryStatus.Submitted);

        if (hasPendingApprovals)
        {
            throw new BusinessRuleException("All timesheet entries must be approved or rejected before closing the period.");
        }

        payPeriod.Status = PayPeriodStatus.Closed;
        await _db.SaveChangesAsync();

        return Ok(ToResponse(payPeriod));
    }

    [HttpPost("{id:int}/process")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PayrollRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayrollRunResponse>> ProcessPayPeriod(int id)
    {
        var run = await _payrollProcessingService.ProcessAsync(id);

        var employeeNames = await _db.Employees
            .Where(e => run.Payslips.Select(p => p.EmployeeId).Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.FullName);

        var payPeriod = await _db.PayPeriods.AsNoTracking().FirstAsync(p => p.Id == id);

        return Ok(new PayrollRunResponse
        {
            Id = run.Id,
            PayPeriodId = run.PayPeriodId,
            ProcessedAtUtc = run.ProcessedAtUtc,
            TotalGrossPay = run.TotalGrossPay,
            TotalTaxAmount = run.TotalTaxAmount,
            TotalNetPay = run.TotalNetPay,
            Payslips = run.Payslips.Select(p => new PayslipResponse
            {
                Id = p.Id,
                PayrollRunId = p.PayrollRunId,
                PayPeriodId = payPeriod.Id,
                PayPeriodStart = payPeriod.StartDate,
                PayPeriodEnd = payPeriod.EndDate,
                EmployeeId = p.EmployeeId,
                EmployeeName = employeeNames.GetValueOrDefault(p.EmployeeId, string.Empty),
                RegularHours = p.RegularHours,
                OvertimeHours = p.OvertimeHours,
                RegularPay = p.RegularPay,
                OvertimePay = p.OvertimePay,
                GrossPay = p.GrossPay,
                TaxAmount = p.TaxAmount,
                NetPay = p.NetPay
            }).ToList()
        });
    }

    [HttpPost("{id:int}/mark-paid")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(PayPeriodResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayPeriodResponse>> MarkPaid(int id, VersionedActionRequest request)
    {
        var payPeriod = await _db.PayPeriods.FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new EntityNotFoundException($"Pay period {id} was not found.");

        EnsureCurrentVersion(payPeriod, request.ExpectedVersion);

        if (payPeriod.Status != PayPeriodStatus.Processed)
        {
            throw new BusinessRuleException($"Pay period {id} must be Processed before it can be marked Paid (current status: {payPeriod.Status}).");
        }

        payPeriod.Status = PayPeriodStatus.Paid;
        await _db.SaveChangesAsync();

        return Ok(ToResponse(payPeriod));
    }

    private static void EnsureCurrentVersion(PayPeriod payPeriod, int expectedVersion)
    {
        if (payPeriod.Version != expectedVersion)
        {
            throw new ConcurrencyConflictException(
                $"Pay period {payPeriod.Id} was changed by another request (expected version {expectedVersion}, current version {payPeriod.Version}). Reload and try again.");
        }
    }

    private static PayPeriodResponse ToResponse(PayPeriod p) => new()
    {
        Id = p.Id,
        StartDate = p.StartDate,
        EndDate = p.EndDate,
        Status = p.Status.ToString(),
        Version = p.Version
    };
}
