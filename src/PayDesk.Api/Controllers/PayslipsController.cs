using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayDesk.Api.Contracts.Common;
using PayDesk.Api.Contracts.Payslips;
using PayDesk.Api.Data;
using PayDesk.Api.Domain.Exceptions;
using PayDesk.Api.Security;

namespace PayDesk.Api.Controllers;

[ApiController]
[Route("api/payslips")]
[Authorize]
public class PayslipsController : ControllerBase
{
    private readonly PayDeskDbContext _db;

    public PayslipsController(PayDeskDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<PayslipResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<PayslipResponse>>> GetPayslips(
        [FromQuery] PageQuery query, [FromQuery] int? employeeId, [FromQuery] int? payPeriodId)
    {
        var payslips = _db.Payslips
            .Include(p => p.Employee)
            .Include(p => p.PayrollRun)
            .ThenInclude(r => r!.PayPeriod)
            .AsQueryable();

        var currentEmployeeId = User.GetEmployeeId();

        if (User.IsInRole("Employee"))
        {
            payslips = payslips.Where(p => p.EmployeeId == currentEmployeeId);
        }
        else if (User.IsInRole("Manager"))
        {
            if (employeeId.HasValue)
            {
                var isDirectReport = await _db.Employees.AnyAsync(e => e.Id == employeeId && e.ManagerId == currentEmployeeId);
                if (!isDirectReport)
                {
                    return Forbid();
                }

                payslips = payslips.Where(p => p.EmployeeId == employeeId);
            }
            else
            {
                payslips = payslips.Where(p => p.Employee!.ManagerId == currentEmployeeId);
            }
        }
        else if (employeeId.HasValue)
        {
            payslips = payslips.Where(p => p.EmployeeId == employeeId);
        }

        if (payPeriodId.HasValue)
        {
            payslips = payslips.Where(p => p.PayrollRun!.PayPeriodId == payPeriodId);
        }

        var totalCount = await payslips.CountAsync();

        var items = await payslips
            .OrderByDescending(p => p.PayrollRun!.ProcessedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => ToResponse(p))
            .ToListAsync();

        return Ok(new PagedResult<PayslipResponse>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(PayslipResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayslipResponse>> GetPayslip(int id)
    {
        var payslip = await _db.Payslips
            .Include(p => p.Employee)
            .Include(p => p.PayrollRun)
            .ThenInclude(r => r!.PayPeriod)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new EntityNotFoundException($"Payslip {id} was not found.");

        var currentEmployeeId = User.GetEmployeeId();
        var isOwner = payslip.EmployeeId == currentEmployeeId;
        var isManagerOfOwner = User.IsInRole("Manager") && payslip.Employee!.ManagerId == currentEmployeeId;

        if (!User.IsInRole("Admin") && !isOwner && !isManagerOfOwner)
        {
            return Forbid();
        }

        return Ok(ToResponse(payslip));
    }

    private static PayslipResponse ToResponse(Domain.Entities.Payslip p) => new()
    {
        Id = p.Id,
        PayrollRunId = p.PayrollRunId,
        PayPeriodId = p.PayrollRun!.PayPeriodId,
        PayPeriodStart = p.PayrollRun.PayPeriod!.StartDate,
        PayPeriodEnd = p.PayrollRun.PayPeriod.EndDate,
        EmployeeId = p.EmployeeId,
        EmployeeName = p.Employee?.FullName ?? string.Empty,
        RegularHours = p.RegularHours,
        OvertimeHours = p.OvertimeHours,
        RegularPay = p.RegularPay,
        OvertimePay = p.OvertimePay,
        GrossPay = p.GrossPay,
        TaxAmount = p.TaxAmount,
        NetPay = p.NetPay
    };
}
