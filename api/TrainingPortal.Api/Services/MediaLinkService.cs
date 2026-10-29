using Microsoft.AspNetCore.DataProtection;

namespace TrainingPortal.Api.Services;

/// <summary>
/// A browser's &lt;video&gt; element cannot send an Authorization header, so video URLs
/// carry a short-lived signed ticket for one user and one content item instead of the
/// login token.
/// </summary>
public class MediaLinkService(IDataProtectionProvider provider)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(6);
    private readonly ITimeLimitedDataProtector _protector =
        provider.CreateProtector("TrainingPortal.Media").ToTimeLimitedDataProtector();

    public string VideoUrl(int contentId, int userId) =>
        $"/api/media/video/{contentId}?t={Uri.EscapeDataString(_protector.Protect($"{contentId}:{userId}", Lifetime))}";

    public bool IsValid(string? ticket, int contentId)
    {
        if (string.IsNullOrEmpty(ticket)) return false;
        try
        {
            var parts = _protector.Unprotect(ticket).Split(':');
            return parts.Length == 2 && parts[0] == contentId.ToString();
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }
}
