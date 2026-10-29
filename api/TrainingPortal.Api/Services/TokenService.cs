using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TrainingPortal.Api.Data;

namespace TrainingPortal.Api.Services;

public class JwtOptions
{
    public string Issuer { get; set; } = "TrainingPortal";
    public string Audience { get; set; } = "TrainingPortal";
    /// <summary>Signing key, at least 32 characters. Set it from a secret store in production.</summary>
    public string Key { get; set; } = "";
    public int LifetimeHours { get; set; } = 8;

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

public class TokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _opt = options.Value;

    public (string Token, DateTime ExpiresAt) Issue(User user)
    {
        var expires = DateTime.UtcNow.AddHours(_opt.LifetimeHours);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
        };
        var token = new JwtSecurityToken(
            _opt.Issuer, _opt.Audience, claims,
            expires: expires,
            signingCredentials: new SigningCredentials(_opt.SigningKey(), SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static int UserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? throw new InvalidOperationException("No user id claim."));
}
