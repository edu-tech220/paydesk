using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PayDesk.Web.Models;
using PayDesk.Web.Services;

namespace PayDesk.Web.Pages.Timesheets;

[Authorize]
public class IndexModel : PageModel
{
    private readonly PayDeskApiClient _api;

    public IndexModel(PayDeskApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    public EntryInput Input { get; set; } = new();

    public List<TimesheetEntryDto> Entries { get; private set; } = new();
    public List<PayPeriodDto> OpenPayPeriods { get; private set; } = new();
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
            await _api.CreateTimesheetEntryAsync(Input.PayPeriodId, Input.WorkDate, Input.HoursWorked);
            SuccessMessage = "Hours submitted.";
            Input = new EntryInput();
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
        var employeeId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var entries = await _api.GetTimesheetEntriesAsync(employeeId: employeeId, pageSize: 100);
        Entries = entries.Items.OrderByDescending(e => e.WorkDate).ToList();

        var periods = await _api.GetPayPeriodsAsync(pageSize: 100);
        OpenPayPeriods = periods.Items.Where(p => p.Status == "Open").OrderBy(p => p.StartDate).ToList();
    }

    public class EntryInput
    {
        [Required]
        public int PayPeriodId { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateOnly WorkDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Range(0.25, 24)]
        public decimal HoursWorked { get; set; } = 8m;
    }
}
