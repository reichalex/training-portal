using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace TrainingPortal.Api.Services;

public class AuthOptions
{
    /// <summary>Allow signing in with an email and a password stored by the portal.</summary>
    public bool LocalLogin { get; set; } = true;
}

/// <summary>
/// Sign-in with Microsoft Entra ID (Azure AD). Enabled when TenantId and ClientId are set.
/// One app registration serves both the Angular app (SPA platform) and the API it calls
/// (Expose an API → scope "access_as_user").
/// </summary>
public class EntraOptions
{
    public string Instance { get; set; } = "https://login.microsoftonline.com/";
    /// <summary>Directory (tenant) ID, or the tenant's domain name.</summary>
    public string? TenantId { get; set; }
    /// <summary>Application (client) ID of the app registration.</summary>
    public string? ClientId { get; set; }
    /// <summary>Scope the browser asks for; defaults to api://{ClientId}/access_as_user.</summary>
    public string? Scope { get; set; }
    /// <summary>Create a learner account the first time someone from the tenant signs in.</summary>
    public bool AutoProvision { get; set; } = true;

    public bool Enabled => !string.IsNullOrWhiteSpace(TenantId) && !string.IsNullOrWhiteSpace(ClientId);
    public string Authority => $"{Instance.TrimEnd('/')}/{TenantId}";
    public string ResolvedScope => string.IsNullOrWhiteSpace(Scope) ? $"api://{ClientId}/access_as_user" : Scope;
    private string? _metadataAddress;

    /// <summary>OpenID metadata the API reads signing keys from. Override only for sovereign clouds or testing.</summary>
    public string MetadataAddress
    {
        get => string.IsNullOrWhiteSpace(_metadataAddress) ? $"{Authority}/v2.0/.well-known/openid-configuration" : _metadataAddress;
        set => _metadataAddress = value;
    }
}

/// <summary>The person a valid Microsoft access token belongs to.</summary>
public record EntraIdentity(string ObjectId, string Email, string DisplayName);

public interface IEntraTokenValidator
{
    /// <summary>Returns who the token is for, or null if it is not a valid user token for this app.</summary>
    Task<EntraIdentity?> ValidateAsync(string accessToken);
}

public class EntraTokenValidator(
    IOptions<EntraOptions> options,
    IConfigurationManager<OpenIdConnectConfiguration> metadata,
    ILogger<EntraTokenValidator> logger) : IEntraTokenValidator
{
    private readonly EntraOptions _opt = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public static IConfigurationManager<OpenIdConnectConfiguration> MetadataFor(EntraOptions opt) =>
        new ConfigurationManager<OpenIdConnectConfiguration>(opt.MetadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            // Plain HTTP is allowed only for a local test identity provider.
            new HttpDocumentRetriever { RequireHttps = !opt.MetadataAddress.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) });

    public async Task<EntraIdentity?> ValidateAsync(string accessToken)
    {
        var config = await metadata.GetConfigurationAsync(CancellationToken.None);
        // The v2 issuer is https://login.microsoftonline.com/{tenant guid}/v2.0. Apps whose
        // manifest still has accessTokenAcceptedVersion = 1 get tokens from sts.windows.net.
        var tenantGuid = config.Issuer.TrimEnd('/').Split('/')[^2];
        var parameters = new TokenValidationParameters
        {
            ValidIssuers = [config.Issuer, $"https://sts.windows.net/{tenantGuid}/"],
            ValidAudiences = [_opt.ClientId, $"api://{_opt.ClientId}"],
            IssuerSigningKeys = config.SigningKeys,
            ClockSkew = TimeSpan.FromMinutes(2),
        };

        var result = await _handler.ValidateTokenAsync(accessToken, parameters);
        if (!result.IsValid)
        {
            // Microsoft rotates signing keys; fetch fresh metadata before the next attempt.
            if (result.Exception is SecurityTokenSignatureKeyNotFoundException) metadata.RequestRefresh();
            logger.LogWarning("Rejected a Microsoft access token: {Reason}", result.Exception?.Message);
            return null;
        }

        string? Claim(string type) => result.ClaimsIdentity.FindFirst(type)?.Value;
        // Only delegated (user) tokens carry scp; app-only tokens must not create accounts.
        if (string.IsNullOrEmpty(Claim("scp")) || Claim("oid") is not { Length: > 0 } oid)
        {
            logger.LogWarning("Rejected a Microsoft access token that is not for a signed-in user.");
            return null;
        }
        var email = Claim("preferred_username") ?? Claim("upn") ?? Claim("email") ?? Claim("unique_name");
        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("Rejected a Microsoft access token without a user name for {Oid}.", oid);
            return null;
        }
        return new EntraIdentity(oid, email.Trim().ToLowerInvariant(), Claim("name") ?? email);
    }
}
