using System.Net;
using System.Net.Http.Json;
using PayDesk.Api.Contracts.Common;
using PayDesk.Api.Contracts.Employees;
using PayDesk.Api.Contracts.PayPeriods;
using PayDesk.Api.Tests.Infrastructure;
using Xunit;

namespace PayDesk.Api.Tests.Integration;

public class AuthorizationAndPaginationTests : IDisposable
{
    private readonly PayDeskWebApplicationFactory _factory = new();

    [Fact]
    public async Task GetEmployees_WithoutAuthentication_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEmployees_AsEmployee_ReturnsForbidden()
    {
        using var client = _factory.CreateClient();
        await client.AuthenticateAsEmployeeAsync();

        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetEmployee_AsEmployeeForAnotherEmployeesRecord_ReturnsForbidden()
    {
        using var client = _factory.CreateClient();
        await client.AuthenticateAsEmployeeAsync();

        // Employee id 1 is the seeded admin account; the logged-in employee is not it.
        var response = await client.GetAsync("/api/employees/1");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreatePayPeriod_AsEmployee_ReturnsForbidden()
    {
        using var client = _factory.CreateClient();
        await client.AuthenticateAsEmployeeAsync();

        var response = await client.PostAsJsonAsync("/api/pay-periods", new CreatePayPeriodRequest
        {
            StartDate = new DateOnly(2028, 1, 1),
            EndDate = new DateOnly(2028, 1, 14)
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetEmployees_AsAdmin_RespectsPageSize()
    {
        using var client = _factory.CreateClient();
        await client.AuthenticateAsAdminAsync();

        var response = await client.GetAsync("/api/employees?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedResult<EmployeeResponse>>();

        Assert.NotNull(page);
        Assert.True(page!.Items.Count <= 2);
        Assert.True(page.TotalCount >= 3); // admin, manager, employee are always seeded.
    }

    public void Dispose() => _factory.Dispose();
}
