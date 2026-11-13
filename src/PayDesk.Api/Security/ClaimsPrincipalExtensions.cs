using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace PayDesk.Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static int GetEmployeeId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Token is missing the subject claim.");
        return int.Parse(value);
    }
}
