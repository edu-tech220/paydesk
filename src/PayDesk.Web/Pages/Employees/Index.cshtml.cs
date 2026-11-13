using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PayDesk.Web.Models;
using PayDesk.Web.Services;

namespace PayDesk.Web.Pages.Employees;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly PayDeskApiClient _api;

    public IndexModel(PayDeskApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    public CreateInput Input { get; set; } = new();

    public List<EmployeeDto> Employees { get; private set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        try
        {
            await _api.CreateEmployeeAsync(new
            {
                employeeCode = Input.EmployeeCode,
                fullName = Input.FullName,
                email = Input.Email,
                password = Input.Password,
                role = Input.Role,
                department = Input.Department,
                hourlyRate = Input.HourlyRate,
                managerId = Input.ManagerId,
                hireDate = Input.HireDate
            });
            SuccessMessage = $"{Input.FullName} was added.";
            Input = new CreateInput();
        }
        catch (ApiProblemException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        var result = await _api.GetEmployeesAsync(pageSize: 100);
        Employees = result.Items.OrderBy(e => e.EmployeeCode).ToList();
    }

    public class CreateInput
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
        [DataType(DataType.Date)]
        public DateOnly HireDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    }
}
