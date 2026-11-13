using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayDesk.Api.Contracts.Common;
using PayDesk.Api.Contracts.TimesheetEntries;
using PayDesk.Api.Data;
using PayDesk.Api.Domain.Entities;
using PayDesk.Api.Domain.Enums;
using PayDesk.Api.Domain.Exceptions;
using PayDesk.Api.Security;

namespace PayDesk.Api.Controllers;

[ApiController]
[Route("api/timesheet-entries")]
[Authorize]
public class TimesheetEntriesController : ControllerBase
{
    private readonly PayDeskDbContext _db;

    public TimesheetEntriesController(PayDeskDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TimesheetEntryResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<TimesheetEntryResponse>>> GetTimesheetEntries(
        [FromQuery] PageQuery query, [FromQuery] int? employeeId, [FromQuery] int? payPeriodId, [FromQuery] string? status)
    {
        var entries = _db.TimesheetEntries.Include(t => t.Employee).AsQueryable();
        var currentEmployeeId = User.GetEmployeeId();

        if (User.IsInRole("Employee"))
        {
            entries = entries.Where(t => t.EmployeeId == currentEmployeeId);
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

                entries = entries.Where(t => t.EmployeeId == employeeId);
            }
            else
            {
                entries = entries.Where(t => t.Employee!.ManagerId == currentEmployeeId);
            }
        }
        else if (employeeId.HasValue)
        {
            entries = entries.Where(t => t.EmployeeId == employeeId);
        }

        if (payPeriodId.HasValue)
        {
            entries = entries.Where(t => t.PayPeriodId == payPeriodId);
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TimesheetEntryStatus>(status, true, out var statusFilter))
        {
            entries = entries.Where(t => t.Status == statusFilter);
        }

        var totalCount = await entries.CountAsync();

        var items = await entries
            .OrderByDescending(t => t.WorkDate)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(t => ToResponse(t))
            .ToListAsync();

        return Ok(new PagedResult<TimesheetEntryResponse>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(TimesheetEntryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TimesheetEntryResponse>> CreateTimesheetEntry(CreateTimesheetEntryRequest request)
    {
        var employeeId = User.GetEmployeeId();

        var payPeriod = await _db.PayPeriods.FirstOrDefaultAsync(p => p.Id == request.PayPeriodId)
            ?? throw new EntityNotFoundException($"Pay period {request.PayPeriodId} was not found.");

        if (payPeriod.Status != PayPeriodStatus.Open)
        {
            throw new BusinessRuleException($"Pay period {request.PayPeriodId} is {payPeriod.Status} and no longer accepts timesheet entries.");
        }

        if (request.WorkDate < payPeriod.StartDate || request.WorkDate > payPeriod.EndDate)
        {
            throw new BusinessRuleException($"{request.WorkDate} is outside pay period {request.PayPeriodId} ({payPeriod.StartDate:d} - {payPeriod.EndDate:d}).");
        }

        var alreadyLogged = await _db.TimesheetEntries
            .AnyAsync(t => t.EmployeeId == employeeId && t.WorkDate == request.WorkDate);

        if (alreadyLogged)
        {
            throw new BusinessRuleException($"Hours for {request.WorkDate} have already been logged.");
        }

        var entry = new TimesheetEntry
        {
            EmployeeId = employeeId,
            PayPeriodId = request.PayPeriodId,
            WorkDate = request.WorkDate,
            HoursWorked = request.HoursWorked,
            Status = TimesheetEntryStatus.Submitted
        };

        _db.TimesheetEntries.Add(entry);
        await _db.SaveChangesAsync();

        await _db.Entry(entry).Reference(t => t.Employee).LoadAsync();
        return CreatedAtAction(nameof(GetTimesheetEntries), null, ToResponse(entry));
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(TimesheetEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TimesheetEntryResponse>> Approve(int id, ApproveTimesheetEntryRequest request)
    {
        var entry = await LoadEntryForDecision(id, request.ExpectedVersion);

        entry.Status = TimesheetEntryStatus.Approved;
        entry.ApprovedByEmployeeId = User.GetEmployeeId();
        entry.RejectionReason = null;

        await _db.SaveChangesAsync();
        return Ok(ToResponse(entry));
    }

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(TimesheetEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TimesheetEntryResponse>> Reject(int id, RejectTimesheetEntryRequest request)
    {
        var entry = await LoadEntryForDecision(id, request.ExpectedVersion);

        entry.Status = TimesheetEntryStatus.Rejected;
        entry.ApprovedByEmployeeId = User.GetEmployeeId();
        entry.RejectionReason = request.Reason;

        await _db.SaveChangesAsync();
        return Ok(ToResponse(entry));
    }

    private async Task<TimesheetEntry> LoadEntryForDecision(int id, int expectedVersion)
    {
        var entry = await _db.TimesheetEntries
            .Include(t => t.Employee)
            .Include(t => t.PayPeriod)
            .FirstOrDefaultAsync(t => t.Id == id)
            ?? throw new EntityNotFoundException($"Timesheet entry {id} was not found.");

        if (!User.IsInRole("Admin") && entry.Employee!.ManagerId != User.GetEmployeeId())
        {
            throw new BusinessRuleException("You can only approve or reject timesheet entries for your direct reports.");
        }

        if (entry.PayPeriod!.Status is PayPeriodStatus.Processed or PayPeriodStatus.Paid)
        {
            throw new BusinessRuleException($"Pay period {entry.PayPeriodId} has already been {entry.PayPeriod.Status} and its timesheet entries are locked.");
        }

        if (entry.Status != TimesheetEntryStatus.Submitted)
        {
            throw new BusinessRuleException($"Timesheet entry {id} was already {entry.Status}.");
        }

        if (entry.Version != expectedVersion)
        {
            throw new ConcurrencyConflictException(
                $"Timesheet entry {id} was changed by another request (expected version {expectedVersion}, current version {entry.Version}). Reload and try again.");
        }

        return entry;
    }

    private static TimesheetEntryResponse ToResponse(TimesheetEntry t) => new()
    {
        Id = t.Id,
        EmployeeId = t.EmployeeId,
        EmployeeName = t.Employee?.FullName ?? string.Empty,
        PayPeriodId = t.PayPeriodId,
        WorkDate = t.WorkDate,
        HoursWorked = t.HoursWorked,
        Status = t.Status.ToString(),
        RejectionReason = t.RejectionReason,
        Version = t.Version
    };
}
