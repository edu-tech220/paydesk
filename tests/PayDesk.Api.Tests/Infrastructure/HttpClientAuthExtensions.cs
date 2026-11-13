using System.Net.Http.Headers;
using System.Net.Http.Json;
using PayDesk.Api.Contracts.Auth;

namespace PayDesk.Api.Tests.Infrastructure;

public static class HttpClientAuthExtensions
{
    public const string AdminEmail = "admin@paydesk.local";
    public const string AdminPassword = "Admin123!";
    public const string ManagerEmail = "manager@paydesk.local";
    public const string ManagerPassword = "Manager123!";
    public const string EmployeeEmail = "employee@paydesk.local";
    public const string EmployeePassword = "Employee123!";

    public static async Task AuthenticateAsAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
    }

    public static Task AuthenticateAsAdminAsync(this HttpClient client) => client.AuthenticateAsAsync(AdminEmail, AdminPassword);

    public static Task AuthenticateAsManagerAsync(this HttpClient client) => client.AuthenticateAsAsync(ManagerEmail, ManagerPassword);

    public static Task AuthenticateAsEmployeeAsync(this HttpClient client) => client.AuthenticateAsAsync(EmployeeEmail, EmployeePassword);
}
