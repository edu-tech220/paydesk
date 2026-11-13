using System.Net;
using System.Net.Http.Json;
using PayDesk.Api.Contracts.PayPeriods;
using PayDesk.Api.Contracts.TimesheetEntries;
using PayDesk.Api.Tests.Infrastructure;
using Xunit;

namespace PayDesk.Api.Tests.Integration;

public class TimesheetWorkflowTests : IDisposable
{
    private readonly PayDeskWebApplicationFactory _factory = new();
    private readonly HttpClient _adminClient;
    private readonly HttpClient _managerClient;
    private readonly HttpClient _employeeClient;

    public TimesheetWorkflowTests()
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

    private async Task<PayPeriodResponse> CreateOpenPayPeriodAsync(DateOnly start, DateOnly end)
    {
        var response = await _adminClient.PostAsJsonAsync("/api/pay-periods", new CreatePayPeriodRequest
        {
            StartDate = start,
            EndDate = end
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PayPeriodResponse>())!;
    }

    [Fact]
    public async Task SubmitEntry_ForOpenPeriod_ReturnsCreatedAndSubmittedStatus()
    {
        await InitAsync();
        var period = await CreateOpenPayPeriodAsync(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 18));

        var response = await _employeeClient.PostAsJsonAsync("/api/timesheet-entries", new CreateTimesheetEntryRequest
        {
            PayPeriodId = period.Id,
            WorkDate = new DateOnly(2026, 1, 6),
            HoursWorked = 8m
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var entry = await response.Content.ReadFromJsonAsync<TimesheetEntryResponse>();
        Assert.Equal("Submitted", entry!.Status);
        Assert.Equal(0, entry.Version);
    }

    [Fact]
    public async Task SubmitEntry_SameDateTwice_ReturnsConflict()
    {
        await InitAsync();
        var period = await CreateOpenPayPeriodAsync(new DateOnly(2026, 2, 2), new DateOnly(2026, 2, 15));
        var request = new CreateTimesheetEntryRequest { PayPeriodId = period.Id, WorkDate = new DateOnly(2026, 2, 3), HoursWorked = 7m };

        var first = await _employeeClient.PostAsJsonAsync("/api/timesheet-entries", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _employeeClient.PostAsJsonAsync("/api/timesheet-entries", request);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task SubmitEntry_OutsidePeriodDateRange_ReturnsConflict()
    {
        await InitAsync();
        var period = await CreateOpenPayPeriodAsync(new DateOnly(2026, 3, 2), new DateOnly(2026, 3, 15));

        var response = await _employeeClient.PostAsJsonAsync("/api/timesheet-entries", new CreateTimesheetEntryRequest
        {
            PayPeriodId = period.Id,
            WorkDate = new DateOnly(2026, 3, 20),
            HoursWorked = 8m
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task SubmitEntry_ForClosedPeriod_ReturnsConflict()
    {
        await InitAsync();
        var period = await CreateOpenPayPeriodAsync(new DateOnly(2026, 4, 6), new DateOnly(2026, 4, 19));

        var close = await _adminClient.PostAsJsonAsync($"/api/pay-periods/{period.Id}/close", new VersionedActionRequest { ExpectedVersion = period.Version });
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);

        var response = await _employeeClient.PostAsJsonAsync("/api/timesheet-entries", new CreateTimesheetEntryRequest
        {
            PayPeriodId = period.Id,
            WorkDate = new DateOnly(2026, 4, 7),
            HoursWorked = 8m
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Approve_AsManager_TransitionsEntryToApproved()
    {
        await InitAsync();
        var period = await CreateOpenPayPeriodAsync(new DateOnly(2026, 5, 4), new DateOnly(2026, 5, 17));
        var created = await _employeeClient.PostAsJsonAsync("/api/timesheet-entries", new CreateTimesheetEntryRequest
        {
            PayPeriodId = period.Id,
            WorkDate = new DateOnly(2026, 5, 5),
            HoursWorked = 8m
        });
        var entry = (await created.Content.ReadFromJsonAsync<TimesheetEntryResponse>())!;

        var response = await _managerClient.PostAsJsonAsync($"/api/timesheet-entries/{entry.Id}/approve", new ApproveTimesheetEntryRequest { ExpectedVersion = entry.Version });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var approved = await response.Content.ReadFromJsonAsync<TimesheetEntryResponse>();
        Assert.Equal("Approved", approved!.Status);
    }

    [Fact]
    public async Task Approve_WithStaleExpectedVersion_ReturnsConflict()
    {
        await InitAsync();
        var period = await CreateOpenPayPeriodAsync(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 14));
        var created = await _employeeClient.PostAsJsonAsync("/api/timesheet-entries", new CreateTimesheetEntryRequest
        {
            PayPeriodId = period.Id,
            WorkDate = new DateOnly(2026, 6, 2),
            HoursWorked = 8m
        });
        var entry = (await created.Content.ReadFromJsonAsync<TimesheetEntryResponse>())!;

        var response = await _managerClient.PostAsJsonAsync($"/api/timesheet-entries/{entry.Id}/approve", new ApproveTimesheetEntryRequest { ExpectedVersion = entry.Version + 99 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Approve_SameEntryTwice_SecondAttemptReturnsConflict()
    {
        await InitAsync();
        var period = await CreateOpenPayPeriodAsync(new DateOnly(2026, 7, 6), new DateOnly(2026, 7, 19));
        var created = await _employeeClient.PostAsJsonAsync("/api/timesheet-entries", new CreateTimesheetEntryRequest
        {
            PayPeriodId = period.Id,
            WorkDate = new DateOnly(2026, 7, 7),
            HoursWorked = 8m
        });
        var entry = (await created.Content.ReadFromJsonAsync<TimesheetEntryResponse>())!;

        var firstApprove = await _managerClient.PostAsJsonAsync($"/api/timesheet-entries/{entry.Id}/approve", new ApproveTimesheetEntryRequest { ExpectedVersion = entry.Version });
        Assert.Equal(HttpStatusCode.OK, firstApprove.StatusCode);
        var approved = (await firstApprove.Content.ReadFromJsonAsync<TimesheetEntryResponse>())!;

        var secondApprove = await _managerClient.PostAsJsonAsync($"/api/timesheet-entries/{entry.Id}/approve", new ApproveTimesheetEntryRequest { ExpectedVersion = approved.Version });

        Assert.Equal(HttpStatusCode.Conflict, secondApprove.StatusCode);
    }

    [Fact]
    public async Task Reject_WithReason_SetsRejectedStatusAndStoresReason()
    {
        await InitAsync();
        var period = await CreateOpenPayPeriodAsync(new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 16));
        var created = await _employeeClient.PostAsJsonAsync("/api/timesheet-entries", new CreateTimesheetEntryRequest
        {
            PayPeriodId = period.Id,
            WorkDate = new DateOnly(2026, 8, 4),
            HoursWorked = 6m
        });
        var entry = (await created.Content.ReadFromJsonAsync<TimesheetEntryResponse>())!;

        var response = await _managerClient.PostAsJsonAsync($"/api/timesheet-entries/{entry.Id}/reject", new RejectTimesheetEntryRequest
        {
            ExpectedVersion = entry.Version,
            Reason = "Hours look incorrect, please resubmit."
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rejected = await response.Content.ReadFromJsonAsync<TimesheetEntryResponse>();
        Assert.Equal("Rejected", rejected!.Status);
        Assert.Equal("Hours look incorrect, please resubmit.", rejected.RejectionReason);
    }

    [Fact]
    public async Task Approve_ByManagerWhoIsNotTheEmployeesManager_ReturnsConflict()
    {
        await InitAsync();
        var period = await CreateOpenPayPeriodAsync(new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 20));

        // Create a second manager and employee pair that is NOT related to the seeded manager.
        var otherManagerResponse = await _adminClient.PostAsJsonAsync("/api/employees", new
        {
            employeeCode = "MGR-002",
            fullName = "Other Manager",
            email = "other.manager@paydesk.local",
            password = "OtherManager123!",
            role = "Manager",
            department = "Sales",
            hourlyRate = 30m,
            hireDate = new DateOnly(2024, 1, 1)
        });
        otherManagerResponse.EnsureSuccessStatusCode();

        var employeeResponse = await _adminClient.PostAsJsonAsync("/api/employees", new
        {
            employeeCode = "EMP-002",
            fullName = "Second Employee",
            email = "second.employee@paydesk.local",
            password = "SecondEmployee123!",
            role = "Employee",
            department = "Sales",
            hourlyRate = 20m,
            hireDate = new DateOnly(2024, 6, 1)
        });
        employeeResponse.EnsureSuccessStatusCode();
        var secondEmployee = await employeeResponse.Content.ReadFromJsonAsync<PayDesk.Api.Contracts.Employees.EmployeeResponse>();

        using var secondEmployeeClient = _factory.CreateClient();
        await secondEmployeeClient.AuthenticateAsAsync("second.employee@paydesk.local", "SecondEmployee123!");

        var created = await secondEmployeeClient.PostAsJsonAsync("/api/timesheet-entries", new CreateTimesheetEntryRequest
        {
            PayPeriodId = period.Id,
            WorkDate = new DateOnly(2026, 9, 8),
            HoursWorked = 8m
        });
        created.EnsureSuccessStatusCode();
        var entry = (await created.Content.ReadFromJsonAsync<TimesheetEntryResponse>())!;

        // The ORIGINAL seeded manager (not EMP-002's manager) tries to approve it.
        var response = await _managerClient.PostAsJsonAsync($"/api/timesheet-entries/{entry.Id}/approve", new ApproveTimesheetEntryRequest { ExpectedVersion = entry.Version });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(secondEmployee);
    }

    public void Dispose()
    {
        _adminClient.Dispose();
        _managerClient.Dispose();
        _employeeClient.Dispose();
        _factory.Dispose();
    }
}
