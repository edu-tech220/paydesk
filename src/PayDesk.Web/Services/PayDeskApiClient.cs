using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PayDesk.Web.Models;

namespace PayDesk.Web.Services;

/// <summary>
/// Thin typed wrapper around the PayDesk.Api HTTP surface. Every call attaches the
/// JWT the frontend received at login (stored as a claim in the auth cookie) as a
/// Bearer token, so authorization is still enforced by the API itself.
/// </summary>
public class PayDeskApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PayDeskApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<LoginResponse> LoginAsync(string email, string password)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/login", new { email, password });
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions))!;
    }

    public Task<PagedResult<EmployeeDto>> GetEmployeesAsync(int page = 1, int pageSize = 50) =>
        GetAsync<PagedResult<EmployeeDto>>($"api/employees?page={page}&pageSize={pageSize}");

    public Task<EmployeeDto> GetEmployeeAsync(int id) => GetAsync<EmployeeDto>($"api/employees/{id}");

    public async Task<EmployeeDto> CreateEmployeeAsync(object request)
    {
        using var response = await SendAuthorized(HttpMethod.Post, "api/employees", request);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<EmployeeDto>(JsonOptions))!;
    }

    public Task<PagedResult<TimesheetEntryDto>> GetTimesheetEntriesAsync(int? employeeId = null, int? payPeriodId = null, string? status = null, int page = 1, int pageSize = 50)
    {
        var query = BuildQuery(("employeeId", employeeId), ("payPeriodId", payPeriodId), ("status", status), ("page", page), ("pageSize", pageSize));
        return GetAsync<PagedResult<TimesheetEntryDto>>($"api/timesheet-entries{query}");
    }

    public async Task CreateTimesheetEntryAsync(int payPeriodId, DateOnly workDate, decimal hoursWorked)
    {
        using var response = await SendAuthorized(HttpMethod.Post, "api/timesheet-entries", new { payPeriodId, workDate, hoursWorked });
        await EnsureSuccess(response);
    }

    public async Task ApproveTimesheetEntryAsync(int id, int expectedVersion)
    {
        using var response = await SendAuthorized(HttpMethod.Post, $"api/timesheet-entries/{id}/approve", new { expectedVersion });
        await EnsureSuccess(response);
    }

    public async Task RejectTimesheetEntryAsync(int id, int expectedVersion, string reason)
    {
        using var response = await SendAuthorized(HttpMethod.Post, $"api/timesheet-entries/{id}/reject", new { expectedVersion, reason });
        await EnsureSuccess(response);
    }

    public Task<PagedResult<PayPeriodDto>> GetPayPeriodsAsync(int page = 1, int pageSize = 50) =>
        GetAsync<PagedResult<PayPeriodDto>>($"api/pay-periods?page={page}&pageSize={pageSize}");

    public async Task CreatePayPeriodAsync(DateOnly startDate, DateOnly endDate)
    {
        using var response = await SendAuthorized(HttpMethod.Post, "api/pay-periods", new { startDate, endDate });
        await EnsureSuccess(response);
    }

    public async Task ClosePayPeriodAsync(int id, int expectedVersion)
    {
        using var response = await SendAuthorized(HttpMethod.Post, $"api/pay-periods/{id}/close", new { expectedVersion });
        await EnsureSuccess(response);
    }

    public async Task<PayrollRunDto> ProcessPayPeriodAsync(int id)
    {
        using var response = await SendAuthorized(HttpMethod.Post, $"api/pay-periods/{id}/process", null);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<PayrollRunDto>(JsonOptions))!;
    }

    public async Task MarkPaidAsync(int id, int expectedVersion)
    {
        using var response = await SendAuthorized(HttpMethod.Post, $"api/pay-periods/{id}/mark-paid", new { expectedVersion });
        await EnsureSuccess(response);
    }

    public Task<PagedResult<PayslipDto>> GetPayslipsAsync(int? employeeId = null, int? payPeriodId = null, int page = 1, int pageSize = 50)
    {
        var query = BuildQuery(("employeeId", employeeId), ("payPeriodId", payPeriodId), ("page", page), ("pageSize", pageSize));
        return GetAsync<PagedResult<PayslipDto>>($"api/payslips{query}");
    }

    private async Task<T> GetAsync<T>(string path)
    {
        using var response = await SendAuthorized(HttpMethod.Get, path, null);
        await EnsureSuccess(response);
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> SendAuthorized(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);

        var token = _httpContextAccessor.HttpContext?.User.FindFirst("jwt")?.Value;
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return await _httpClient.SendAsync(request);
    }

    private static async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? detail = null;
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (problem.TryGetProperty("detail", out var detailProperty))
            {
                detail = detailProperty.GetString();
            }
        }
        catch
        {
            // Response body wasn't a ProblemDetails payload; fall back to the status line below.
        }

        throw new ApiProblemException(detail ?? $"Request failed with status {(int)response.StatusCode}.", (int)response.StatusCode);
    }

    private static string BuildQuery(params (string Name, object? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => p.Value is not null && p.Value as string != string.Empty)
            .Select(p => $"{Uri.EscapeDataString(p.Name)}={Uri.EscapeDataString(p.Value!.ToString()!)}");

        var joined = string.Join("&", parts);
        return joined.Length == 0 ? string.Empty : $"?{joined}";
    }
}
