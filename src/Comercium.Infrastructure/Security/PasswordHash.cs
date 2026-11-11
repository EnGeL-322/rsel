using System.Security.Cryptography;

namespace Comercium.Infrastructure.Security;

internal static class PasswordHash
{
    private const int Iterations = 600_000;
    private const int HashLength = 32;

    public static string Create(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashLength);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string encoded)
    {
        var parts = encoded.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out var iterations)
            || iterations < 100_000 || iterations > 2_000_000)
            return false;

        try
        {
            var expected = Convert.FromBase64String(parts[3]);
            var salt = Convert.FromBase64String(parts[2]);
            if (expected.Length != HashLength || salt.Length != 16)
                return false;
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashLength);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
