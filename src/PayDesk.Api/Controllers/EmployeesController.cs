using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayDesk.Api.Contracts.Common;
using PayDesk.Api.Contracts.Employees;
using PayDesk.Api.Data;
using PayDesk.Api.Domain.Entities;
using PayDesk.Api.Domain.Enums;
using PayDesk.Api.Domain.Exceptions;
using PayDesk.Api.Security;

namespace PayDesk.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly PayDeskDbContext _db;

    public EmployeesController(PayDeskDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(PagedResult<EmployeeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<EmployeeResponse>>> GetEmployees([FromQuery] PageQuery query, [FromQuery] bool? isActive)
    {
        var employees = _db.Employees.Include(e => e.Manager).AsQueryable();

        if (isActive.HasValue)
        {
            employees = employees.Where(e => e.IsActive == isActive.Value);
        }

        var totalCount = await employees.CountAsync();

        var items = await employees
            .OrderBy(e => e.EmployeeCode)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => ToResponse(e))
            .ToListAsync();

        return Ok(new PagedResult<EmployeeResponse>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeResponse>> GetEmployee(int id)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Manager") && User.GetEmployeeId() != id)
        {
            return Forbid();
        }

        var employee = await _db.Employees.Include(e => e.Manager).FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new EntityNotFoundException($"Employee {id} was not found.");

        return Ok(ToResponse(employee));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<EmployeeResponse>> CreateEmployee(CreateEmployeeRequest request)
    {
        if (!Enum.TryParse<EmployeeRole>(request.Role, ignoreCase: true, out var role))
        {
            return Problem(
                detail: $"Role must be one of: {string.Join(", ", Enum.GetNames<EmployeeRole>())}.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid role");
        }

        var (hash, salt) = PasswordHasher.Hash(request.Password);

        var employee = new Employee
        {
            EmployeeCode = request.EmployeeCode,
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role,
            Department = request.Department,
            HourlyRate = request.HourlyRate,
            ManagerId = request.ManagerId,
            HireDate = request.HireDate,
            IsActive = true
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEmployee), new { id = employee.Id }, ToResponse(employee));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EmployeeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeResponse>> UpdateEmployee(int id, UpdateEmployeeRequest request)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new EntityNotFoundException($"Employee {id} was not found.");

        employee.FullName = request.FullName;
        employee.Department = request.Department;
        employee.HourlyRate = request.HourlyRate;
        employee.ManagerId = request.ManagerId;
        employee.IsActive = request.IsActive;

        await _db.SaveChangesAsync();

        await _db.Entry(employee).Reference(e => e.Manager).LoadAsync();
        return Ok(ToResponse(employee));
    }

    private static EmployeeResponse ToResponse(Employee e) => new()
    {
        Id = e.Id,
        EmployeeCode = e.EmployeeCode,
        FullName = e.FullName,
        Email = e.Email,
        Role = e.Role.ToString(),
        Department = e.Department,
        HourlyRate = e.HourlyRate,
        ManagerId = e.ManagerId,
        ManagerName = e.Manager?.FullName,
        HireDate = e.HireDate,
        IsActive = e.IsActive
    };
}
