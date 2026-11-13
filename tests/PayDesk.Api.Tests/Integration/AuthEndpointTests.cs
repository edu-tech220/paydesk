using System.Net;
using System.Net.Http.Json;
using PayDesk.Api.Contracts.Auth;
using PayDesk.Api.Tests.Infrastructure;
using Xunit;

namespace PayDesk.Api.Tests.Integration;

public class AuthEndpointTests : IDisposable
{
    private readonly PayDeskWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public AuthEndpointTests()
    {
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithSeededAdminCredentials_ReturnsToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "admin@paydesk.local",
            Password = "Admin123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.Equal("Admin", body.Role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "admin@paydesk.local",
            Password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "nobody@paydesk.local",
            Password = "whatever123"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
