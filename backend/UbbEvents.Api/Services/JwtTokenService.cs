using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UbbEvents.Api.DTOs;
using UbbEvents.Api.Models;

namespace UbbEvents.Api.Services;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SigningKey { get; set; } = "";
    public int ExpirationMinutes { get; set; } = 60;
    public static bool IsValid(JwtOptions options) => !string.IsNullOrWhiteSpace(options.Issuer)
        && !string.IsNullOrWhiteSpace(options.Audience) && !string.IsNullOrWhiteSpace(options.SigningKey)
        && Encoding.UTF8.GetByteCount(options.SigningKey) >= 32
        && options.ExpirationMinutes is >= 5 and <= 1440;
}

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock)
{
    public AuthResponse Issue(User user)
    {
        var config = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(config.ExpirationMinutes);
        var token = new JwtSecurityToken(config.Issuer, config.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
             new Claim("role", user.GlobalRole.ToString()),
             new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            now, expires, new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.SigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, user.ToResponse());
    }
}
