using PayDesk.Api.Domain.Entities;

namespace PayDesk.Api.Security;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateToken(Employee employee);
}
