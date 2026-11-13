using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PayDesk.Web.Models;
using PayDesk.Web.Services;

namespace PayDesk.Web.Pages.Approvals;

[Authorize(Roles = "Admin,Manager")]
public class IndexModel : PageModel
{
    private readonly PayDeskApiClient _api;

    public IndexModel(PayDeskApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    public DecisionInput Input { get; set; } = new();

    public List<TimesheetEntryDto> PendingEntries { get; private set; } = new();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostApproveAsync(int id, int expectedVersion)
    {
        try
        {
            await _api.ApproveTimesheetEntryAsync(id, expectedVersion);
            SuccessMessage = "Entry approved.";
        }
        catch (ApiProblemException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRejectAsync(int id, int expectedVersion, string reason)
    {
        try
        {
            await _api.RejectTimesheetEntryAsync(id, expectedVersion, reason);
            SuccessMessage = "Entry rejected.";
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
        var entries = await _api.GetTimesheetEntriesAsync(status: "Submitted", pageSize: 100);
        PendingEntries = entries.Items.OrderBy(e => e.WorkDate).ToList();
    }

    public class DecisionInput
    {
        [Required]
        public string Reason { get; set; } = string.Empty;
    }
}
