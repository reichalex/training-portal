using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Services;

namespace TrainingPortal.Api.Controllers;

/// <summary>
/// Serves uploaded files. These endpoints are anonymous at the HTTP level because the
/// browser fetches them from &lt;video&gt; and &lt;img&gt; tags: videos need a signed
/// ticket from the content endpoint, and images have unguessable names.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/media")]
public partial class MediaController(PortalDbContext db, IFileStorage storage, MediaLinkService links) : ControllerBase
{
    [HttpGet("video/{id:int}")]
    public async Task<IActionResult> Video(int id, [FromQuery] string? t)
    {
        if (!links.IsValid(t, id)) return Unauthorized();
        var item = await db.ContentItems.FindAsync(id);
        if (item?.VideoFileKey is null) return NotFound();
        var stream = storage.OpenRead(item.VideoFileKey);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(stream, item.VideoContentType ?? "video/mp4", enableRangeProcessing: true);
    }

    [HttpGet("images/{name}")]
    public IActionResult Image(string name)
    {
        var match = ImageName().Match(name);
        if (!match.Success) return NotFound();
        var stream = storage.OpenRead($"images/{name}");
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(stream, ImageTypes[match.Groups[1].Value]);
    }

    public static readonly Dictionary<string, string> ImageTypes = new()
    {
        ["png"] = "image/png", ["jpg"] = "image/jpeg", ["jpeg"] = "image/jpeg",
        ["gif"] = "image/gif", ["webp"] = "image/webp",
    };

    public static readonly Dictionary<string, string> VideoTypes = new()
    {
        ["mp4"] = "video/mp4", ["m4v"] = "video/mp4", ["webm"] = "video/webm", ["mov"] = "video/quicktime",
    };

    [GeneratedRegex("^[0-9a-f]{32}\\.(png|jpg|jpeg|gif|webp)$")]
    private static partial Regex ImageName();
}
