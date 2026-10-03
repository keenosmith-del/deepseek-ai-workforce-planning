using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Workforce.Domain;

namespace Workforce.Application;

public static class Passwords
{
    public static string Hash(string value)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(salt)
            + ":"
            + Convert.ToBase64String(
                Rfc2898DeriveBytes.Pbkdf2(value, salt, 210000, HashAlgorithmName.SHA256, 32)
            );
    }

    public static bool Verify(string value, string stored)
    {
        try
        {
            var p = stored.Split(':');
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(p[1]),
                Rfc2898DeriveBytes.Pbkdf2(
                    value,
                    Convert.FromBase64String(p[0]),
                    210000,
                    HashAlgorithmName.SHA256,
                    32
                )
            );
        }
        catch
        {
            return false;
        }
    }
}

public class TokenService(IConfiguration config)
{
    public string Issue(User u)
    {
        var token = new JwtSecurityToken(
            "workforce",
            "workforce-ui",
            [
                new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()),
                new Claim(ClaimTypes.Role, u.Role),
            ],
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),
                SecurityAlgorithms.HmacSha256
            )
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
