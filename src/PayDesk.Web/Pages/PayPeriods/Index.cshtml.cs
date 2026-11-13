using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PayDesk.Web.Models;
using PayDesk.Web.Services;

namespace PayDesk.Web.Pages.PayPeriods;

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

    public List<PayPeriodDto> PayPeriods { get; private set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        try
        {
            await _api.CreatePayPeriodAsync(Input.StartDate, Input.EndDate);
            SuccessMessage = "Pay period created.";
        }
        catch (ApiProblemException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCloseAsync(int id, int expectedVersion)
    {
        try
        {
            await _api.ClosePayPeriodAsync(id, expectedVersion);
            SuccessMessage = $"Pay period {id} closed.";
        }
        catch (ApiProblemException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostProcessAsync(int id)
    {
        try
        {
            var run = await _api.ProcessPayPeriodAsync(id);
            SuccessMessage = $"Payroll processed: {run.Payslips.Count} payslip(s), net total {run.TotalNetPay:C}.";
        }
        catch (ApiProblemException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostMarkPaidAsync(int id, int expectedVersion)
    {
        try
        {
            await _api.MarkPaidAsync(id, expectedVersion);
            SuccessMessage = $"Pay period {id} marked as paid.";
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
        var result = await _api.GetPayPeriodsAsync(pageSize: 100);
        PayPeriods = result.Items.OrderByDescending(p => p.StartDate).ToList();
    }

    public class CreateInput
    {
        [Required]
        [DataType(DataType.Date)]
        public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        [Required]
        [DataType(DataType.Date)]
        public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(13));
    }
}
