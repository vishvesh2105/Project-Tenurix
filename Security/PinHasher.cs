using System;
using System.Security.Cryptography;

namespace Capstone.Api.Security;

public static class PinHasher
{
    private const int Iterations = 100_000;
    private const int SaltSize = 16;   // bytes
    private const int KeySize = 32;    // bytes

    public static (string hash, string salt) Hash(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize
        );

        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }



    public static bool Verify(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(storedSalt))
            return false;

        var saltBytes = Convert.FromBase64String(storedSalt);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize
        );

        var candidate = Convert.ToBase64String(hashBytes);

        // constant-time compare
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(candidate),
            Convert.FromBase64String(storedHash)
        );
    }


}
