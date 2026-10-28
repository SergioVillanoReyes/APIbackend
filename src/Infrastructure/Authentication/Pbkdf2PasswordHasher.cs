using System.Security.Cryptography;
using Application.Authentication;

namespace Infrastructure.Authentication;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 600_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int MaximumAcceptedIterations = 5_000_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var derivedKey = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        try
        {
            return string.Join(
                '$',
                "pbkdf2-sha256",
                "v1",
                Iterations,
                Convert.ToBase64String(salt),
                Convert.ToBase64String(derivedKey));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(derivedKey);
        }
    }

    public bool Verify(string password, string passwordHash)
    {
        var parts = passwordHash.Split('$');
        if (parts.Length != 5
            || parts[0] != "pbkdf2-sha256"
            || parts[1] != "v1"
            || !int.TryParse(parts[2], out var iterations)
            || iterations is < 100_000 or > MaximumAcceptedIterations)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[3]);
            var expectedHash = Convert.FromBase64String(parts[4]);
            if (salt.Length != SaltSize || expectedHash.Length != HashSize)
            {
                return false;
            }

            var actualHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            try
            {
                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actualHash);
                CryptographicOperations.ZeroMemory(expectedHash);
            }
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
