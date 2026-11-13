using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PayDesk.Api.Contracts.Auth;
using PayDesk.Api.Data;
using PayDesk.Api.Security;

namespace PayDesk.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly PayDeskDbContext _db;
    private readonly IJwtTokenService _tokenService;

    public AuthController(PayDeskDbContext db, IJwtTokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var employee = await _db.Employees
            .FirstOrDefaultAsync(e => e.Email == request.Email && e.IsActive);

        if (employee is null || !PasswordHasher.Verify(request.Password, employee.PasswordHash, employee.PasswordSalt))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid credentials",
                Detail = "Email or password is incorrect."
            });
        }

        var (token, expiresAtUtc) = _tokenService.CreateToken(employee);

        return Ok(new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = expiresAtUtc,
            EmployeeId = employee.Id,
            FullName = employee.FullName,
            Role = employee.Role.ToString()
        });
    }
}
