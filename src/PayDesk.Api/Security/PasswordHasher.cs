using System.Security.Cryptography;

namespace PayDesk.Api.Security;

/// <summary>
/// PBKDF2 (RFC 2898) password hashing with a random salt per employee and a fixed
/// iteration count. Deliberately not using ASP.NET Core Identity's hasher so the
/// salt/hash split is explicit and storable as two separate byte[] columns.
/// </summary>
public static class PasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public static (byte[] Hash, byte[] Salt) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSizeBytes);
        return (hash, salt);
    }

    public static bool Verify(string password, byte[] hash, byte[] salt)
    {
        var computed = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSizeBytes);
        return CryptographicOperations.FixedTimeEquals(computed, hash);
    }
}
