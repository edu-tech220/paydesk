using System.Net;
using System.Net.Http.Json;
using PayDesk.Api.Contracts.Common;
using PayDesk.Api.Contracts.PayPeriods;
using PayDesk.Api.Contracts.Payslips;
using PayDesk.Api.Contracts.TimesheetEntries;
using PayDesk.Api.Tests.Infrastructure;
using Xunit;

namespace PayDesk.Api.Tests.Integration;

public class PayPeriodLifecycleTests : IDisposable
{
    private readonly PayDeskWebApplicationFactory _factory = new();
    private readonly HttpClient _adminClient;
    private readonly HttpClient _managerClient;
    private readonly HttpClient _employeeClient;

    public PayPeriodLifecycleTests()
    {
        _adminClient = _factory.CreateClient();
        _managerClient = _factory.CreateClient();
        _employeeClient = _factory.CreateClient();
    }

    private async Task InitAsync()
    {
        await _adminClient.AuthenticateAsAdminAsync();
        await _managerClient.AuthenticateAsManagerAsync();
        await _employeeClient.AuthenticateAsEmployeeAsync();
    }

    private async Task<PayPeriodResponse> CreatePayPeriodAsync(DateOnly start, DateOnly end)
    {
        var response = await _adminClient.PostAsJsonAsync("/api/pay-periods", new CreatePayPeriodRequest { StartDate = start, EndDate = end });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PayPeriodResponse>())!;
    }

    private async Task<TimesheetEntryResponse> SubmitAndApproveAsync(int payPeriodId, DateOnly workDate, decimal hours)
    {
        var created = await _employeeClient.PostAsJsonAsync("/api/timesheet-entries", new CreateTimesheetEntryRequest
        {
            PayPeriodId = payPeriodId,
            WorkDate = workDate,
            HoursWorked = hours
        });
        created.EnsureSuccessStatusCode();
        var entry = (await created.Content.ReadFromJsonAsync<TimesheetEntryResponse>())!;

        var approved = await _managerClient.PostAsJsonAsync($"/api/timesheet-entries/{entry.Id}/approve", new ApproveTimesheetEntryRequest { ExpectedVersion = entry.Version });
        approved.EnsureSuccessStatusCode();
        return (await approved.Content.ReadFromJsonAsync<TimesheetEntryResponse>())!;
    }

    [Fact]
    public async Task CreatePayPeriod_OverlappingAnExistingOne_ReturnsConflict()
    {
        await InitAsync();
        await CreatePayPeriodAsync(new DateOnly(2027, 2, 1), new DateOnly(2027, 2, 14));

        var response = await _adminClient.PostAsJsonAsync("/api/pay-periods", new CreatePayPeriodRequest
        {
            StartDate = new DateOnly(2027, 2, 10),
            EndDate = new DateOnly(2027, 2, 23)
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ClosePayPeriod_WithPendingApprovals_ReturnsConflict()
    {
        await InitAsync();
        var period = await CreatePayPeriodAsync(new DateOnly(2027, 3, 1), new DateOnly(2027, 3, 14));

        await _employeeClient.PostAsJsonAsync("/api/timesheet-entries", new CreateTimesheetEntryRequest
        {
            PayPeriodId = period.Id,
            WorkDate = new DateOnly(2027, 3, 2),
            HoursWorked = 8m
        });

        var response = await _adminClient.PostAsJsonAsync($"/api/pay-periods/{period.Id}/close", new VersionedActionRequest { ExpectedVersion = period.Version });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ClosePayPeriod_AfterAllEntriesResolved_Succeeds()
    {
        await InitAsync();
        var period = await CreatePayPeriodAsync(new DateOnly(2027, 4, 5), new DateOnly(2027, 4, 18));
        await SubmitAndApproveAsync(period.Id, new DateOnly(2027, 4, 6), 8m);

        var response = await _adminClient.PostAsJsonAsync($"/api/pay-periods/{period.Id}/close", new VersionedActionRequest { ExpectedVersion = period.Version });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var closed = await response.Content.ReadFromJsonAsync<PayPeriodResponse>();
        Assert.Equal("Closed", closed!.Status);
    }

    [Fact]
    public async Task ProcessPayroll_BeforePeriodIsClosed_ReturnsConflict()
    {
        await InitAsync();
        var period = await CreatePayPeriodAsync(new DateOnly(2027, 5, 3), new DateOnly(2027, 5, 16));

        var response = await _adminClient.PostAsync($"/api/pay-periods/{period.Id}/process", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ProcessPayroll_ComputesOvertimeAndProgressiveTaxCorrectly()
    {
        await InitAsync();
        // Monday 2027-01-04 through Sunday 2027-01-10 is a single ISO week.
        var period = await CreatePayPeriodAsync(new DateOnly(2027, 1, 4), new DateOnly(2027, 1, 10));

        // 5 weekdays x 9h = 45h: 8h/day regular (40h, at the weekly cap) + 1h/day
        // daily overtime (5h). Employee hourly rate is seeded at 18.75.
        for (var day = 0; day < 5; day++)
        {
            await SubmitAndApproveAsync(period.Id, new DateOnly(2027, 1, 4).AddDays(day), 9m);
        }

        await _adminClient.PostAsJsonAsync($"/api/pay-periods/{period.Id}/close", new VersionedActionRequest { ExpectedVersion = period.Version });

        var response = await _adminClient.PostAsync($"/api/pay-periods/{period.Id}/process", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var run = await response.Content.ReadFromJsonAsync<PayrollRunResponse>();
        var slip = Assert.Single(run!.Payslips);

        Assert.Equal(40m, slip.RegularHours);
        Assert.Equal(5m, slip.OvertimeHours);
        Assert.Equal(750.00m, slip.RegularPay);
        Assert.Equal(140.63m, slip.OvertimePay);
        Assert.Equal(890.63m, slip.GrossPay);
        Assert.Equal(44.53m, slip.TaxAmount);
        Assert.Equal(846.10m, slip.NetPay);
    }

    [Fact]
    public async Task ProcessPayroll_CalledTwiceForSamePeriod_IsIdempotent()
    {
        await InitAsync();
        var period = await CreatePayPeriodAsync(new DateOnly(2027, 6, 7), new DateOnly(2027, 6, 20));
        await SubmitAndApproveAsync(period.Id, new DateOnly(2027, 6, 8), 8m);
        await _adminClient.PostAsJsonAsync($"/api/pay-periods/{period.Id}/close", new VersionedActionRequest { ExpectedVersion = period.Version });

        var first = await _adminClient.PostAsync($"/api/pay-periods/{period.Id}/process", null);
        var second = await _adminClient.PostAsync($"/api/pay-periods/{period.Id}/process", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var firstRun = await first.Content.ReadFromJsonAsync<PayrollRunResponse>();
        var secondRun = await second.Content.ReadFromJsonAsync<PayrollRunResponse>();

        Assert.Equal(firstRun!.Id, secondRun!.Id);
        Assert.Equal(firstRun.TotalNetPay, secondRun.TotalNetPay);
        Assert.Single(secondRun.Payslips);

        var payslipsResponse = await _adminClient.GetAsync($"/api/payslips?payPeriodId={period.Id}&pageSize=50");
        var payslips = await payslipsResponse.Content.ReadFromJsonAsync<PagedResult<PayslipResponse>>();
        Assert.Single(payslips!.Items);
    }

    [Fact]
    public async Task MarkPaid_BeforeProcessing_ReturnsConflict()
    {
        await InitAsync();
        var period = await CreatePayPeriodAsync(new DateOnly(2027, 7, 5), new DateOnly(2027, 7, 18));
        await _adminClient.PostAsJsonAsync($"/api/pay-periods/{period.Id}/close", new VersionedActionRequest { ExpectedVersion = period.Version });

        var response = await _adminClient.PostAsJsonAsync($"/api/pay-periods/{period.Id}/mark-paid", new VersionedActionRequest { ExpectedVersion = period.Version + 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task MarkPaid_AfterProcessing_Succeeds()
    {
        await InitAsync();
        var period = await CreatePayPeriodAsync(new DateOnly(2027, 8, 2), new DateOnly(2027, 8, 15));
        await SubmitAndApproveAsync(period.Id, new DateOnly(2027, 8, 3), 8m);

        var closeResponse = await _adminClient.PostAsJsonAsync($"/api/pay-periods/{period.Id}/close", new VersionedActionRequest { ExpectedVersion = period.Version });
        var closed = (await closeResponse.Content.ReadFromJsonAsync<PayPeriodResponse>())!;

        await _adminClient.PostAsync($"/api/pay-periods/{period.Id}/process", null);

        var getResponse = await _adminClient.GetAsync($"/api/pay-periods/{period.Id}");
        var processed = (await getResponse.Content.ReadFromJsonAsync<PayPeriodResponse>())!;

        var response = await _adminClient.PostAsJsonAsync($"/api/pay-periods/{period.Id}/mark-paid", new VersionedActionRequest { ExpectedVersion = processed.Version });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paid = await response.Content.ReadFromJsonAsync<PayPeriodResponse>();
        Assert.Equal("Paid", paid!.Status);
        Assert.True(closed.Version >= 0);
    }

    public void Dispose()
    {
        _adminClient.Dispose();
        _managerClient.Dispose();
        _employeeClient.Dispose();
        _factory.Dispose();
    }
}
