using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using TrainingPortal.Api.Services;

namespace TrainingPortal.Api.Tests;

/// <summary>Validates tokens signed with a local key standing in for Microsoft's.</summary>
public sealed class EntraTokenValidatorTests
{
    private const string Tenant = "11111111-2222-3333-4444-555555555555";
    private const string ClientId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    private static readonly string V2Issuer = $"https://login.microsoftonline.com/{Tenant}/v2.0";

    private readonly RsaSecurityKey _key = new(RSA.Create(2048)) { KeyId = "test-key" };
    private readonly EntraTokenValidator _validator;

    public EntraTokenValidatorTests()
    {
        var metadata = new OpenIdConnectConfiguration { Issuer = V2Issuer };
        metadata.SigningKeys.Add(_key);
        _validator = new EntraTokenValidator(
            Options.Create(new EntraOptions { TenantId = Tenant, ClientId = ClientId }),
            new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata),
            NullLogger<EntraTokenValidator>.Instance);
    }

    private string Token(string? issuer = null, string? audience = null, DateTime? expires = null,
        SecurityKey? signingKey = null, bool userToken = true)
    {
        var claims = new Dictionary<string, object>
        {
            ["oid"] = "user-object-id",
            ["tid"] = Tenant,
            ["preferred_username"] = "Pat.Lee@Contoso.com",
            ["name"] = "Pat Lee",
        };
        if (userToken) claims["scp"] = "access_as_user";
        var exp = expires ?? DateTime.UtcNow.AddHours(1);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer ?? V2Issuer,
            Audience = audience ?? ClientId,
            Claims = claims,
            NotBefore = exp.AddHours(-2),
            IssuedAt = exp.AddHours(-2),
            Expires = exp,
            SigningCredentials = new SigningCredentials(signingKey ?? _key, SecurityAlgorithms.RsaSha256),
        });
    }

    [Fact]
    public async Task Accepts_a_user_token_for_this_app()
    {
        var who = await _validator.ValidateAsync(Token());
        Assert.Equal(new EntraIdentity("user-object-id", "pat.lee@contoso.com", "Pat Lee"), who);
    }

    [Fact]
    public async Task Accepts_v1_tokens_and_the_api_uri_audience()
    {
        Assert.NotNull(await _validator.ValidateAsync(Token(issuer: $"https://sts.windows.net/{Tenant}/")));
        Assert.NotNull(await _validator.ValidateAsync(Token(audience: $"api://{ClientId}")));
    }

    [Fact]
    public async Task Rejects_tokens_for_another_tenant_or_app()
    {
        Assert.Null(await _validator.ValidateAsync(
            Token(issuer: "https://login.microsoftonline.com/99999999-0000-0000-0000-000000000000/v2.0")));
        Assert.Null(await _validator.ValidateAsync(Token(audience: "https://graph.microsoft.com")));
    }

    [Fact]
    public async Task Rejects_expired_forged_and_app_only_tokens()
    {
        Assert.Null(await _validator.ValidateAsync(Token(expires: DateTime.UtcNow.AddMinutes(-10))));
        Assert.Null(await _validator.ValidateAsync(Token(signingKey: new RsaSecurityKey(RSA.Create(2048)) { KeyId = "test-key" })));
        Assert.Null(await _validator.ValidateAsync(Token(userToken: false)));
        Assert.Null(await _validator.ValidateAsync("not-a-token"));
    }
}
