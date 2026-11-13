using PayDesk.Api.Domain.Enums;

namespace PayDesk.Api.Domain.Entities;

public class Employee
{
    public int Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();
    public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();
    public EmployeeRole Role { get; set; } = EmployeeRole.Employee;
    public string Department { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    public int? ManagerId { get; set; }
    public Employee? Manager { get; set; }
    public DateOnly HireDate { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<TimesheetEntry> TimesheetEntries { get; set; } = new List<TimesheetEntry>();
}
