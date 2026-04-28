using System.Security.Cryptography;
using System.Text;

namespace AppointmentSystem.Bll.Helpers;

public static class PasswordHasher
{
    public static (string Hash, string Salt) Hash(string password)
    {
        using var hmac = new HMACSHA512();
        return (
            Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password))),
            Convert.ToBase64String(hmac.Key));
    }

    public static bool Verify(string password, string storedHash, string storedSalt)
    {
        using var hmac = new HMACSHA512(Convert.FromBase64String(storedSalt));
        var computed = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));
        return computed == storedHash;
    }
}
