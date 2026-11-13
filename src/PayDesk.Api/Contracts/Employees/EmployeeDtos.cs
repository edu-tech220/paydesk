using System.ComponentModel.DataAnnotations;

namespace PayDesk.Api.Contracts.Employees;

public class EmployeeResponse
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

public class CreateEmployeeRequest
{
    [Required, MaxLength(20)]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Employee";

    [Required, MaxLength(100)]
    public string Department { get; set; } = string.Empty;

    [Range(0, 10000)]
    public decimal HourlyRate { get; set; }

    public int? ManagerId { get; set; }

    [Required]
    public DateOnly HireDate { get; set; }
}

public class UpdateEmployeeRequest
{
    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Department { get; set; } = string.Empty;

    [Range(0, 10000)]
    public decimal HourlyRate { get; set; }

    public int? ManagerId { get; set; }

    public bool IsActive { get; set; }
}
