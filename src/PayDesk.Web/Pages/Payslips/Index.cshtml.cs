using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PayDesk.Web.Models;
using PayDesk.Web.Services;

namespace PayDesk.Web.Pages.Payslips;

[Authorize]
public class IndexModel : PageModel
{
    private readonly PayDeskApiClient _api;

    public IndexModel(PayDeskApiClient api)
    {
        _api = api;
    }

    public List<PayslipDto> Payslips { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var result = await _api.GetPayslipsAsync(pageSize: 100);
        Payslips = result.Items.OrderByDescending(p => p.PayPeriodStart).ToList();
    }
}
