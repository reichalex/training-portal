using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Dtos;
using TrainingPortal.Api.Services;

namespace TrainingPortal.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/content")]
public class AdminContentController(PortalDbContext db, IFileStorage storage, IOptions<StorageOptions> storageOptions)
    : ControllerBase
{
    [HttpGet]
    public async Task<List<AdminContentDto>> List([FromQuery] int? moduleId, [FromQuery] bool includeSubmodules = false)
    {
        var catalog = await ModuleCatalog.LoadAsync(db);
        var query = db.ContentItems.AsNoTracking().Include(c => c.CreatedBy).AsQueryable();
        if (moduleId is int m)
        {
            var ids = includeSubmodules ? catalog.Subtree(m) : [m];
            query = query.Where(c => ids.Contains(c.ModuleId));
        }
        var items = await query.OrderBy(c => c.ModuleId).ThenBy(c => c.SortOrder).ThenBy(c => c.Title).ToListAsync();
        return await ToDtos(catalog, items);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminContentDto>> Get(int id)
    {
        var item = await db.ContentItems.AsNoTracking().Include(c => c.CreatedBy).FirstOrDefaultAsync(c => c.Id == id);
        if (item is null) return NotFound();
        return (await ToDtos(await ModuleCatalog.LoadAsync(db), [item]))[0];
    }

    [HttpPost]
    public async Task<ActionResult<AdminContentDto>> Create(ContentSaveRequest req)
    {
        if (!await db.Modules.AnyAsync(m => m.Id == req.ModuleId))
            return BadRequest(new { message = "Module not found." });
        var item = new ContentItem { CreatedById = User.UserId() };
        Apply(item, req);
        db.ContentItems.Add(item);
        await db.SaveChangesAsync();
        return await Get(item.Id);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AdminContentDto>> Update(int id, ContentSaveRequest req)
    {
        var item = await db.ContentItems.FindAsync(id);
        if (item is null) return NotFound();
        if (!await db.Modules.AnyAsync(m => m.Id == req.ModuleId))
            return BadRequest(new { message = "Module not found." });
        if (item.Type != req.Type && await db.ContentProgress.AnyAsync(p => p.ContentItemId == id))
            return Conflict(new { message = "People have already viewed this item, so it cannot change between video and article." });
        Apply(item, req);
        await db.SaveChangesAsync();
        return await Get(id);
    }

    /// <summary>Deletes the item along with its viewing history and its place in any course.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await db.ContentItems.FindAsync(id);
        if (item is null) return NotFound();
        var fileKey = item.VideoFileKey;
        db.ContentItems.Remove(item);
        await db.SaveChangesAsync();
        if (fileKey is not null) await storage.DeleteAsync(fileKey);
        return NoContent();
    }

    /// <summary>Uploads (or replaces) the video file of a video item.</summary>
    [HttpPost("{id:int}/video")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]
    public async Task<ActionResult<AdminContentDto>> UploadVideo(int id, IFormFile file, CancellationToken ct)
    {
        var item = await db.ContentItems.FindAsync([id], ct);
        if (item is null) return NotFound();
        if (item.Type != ContentType.Video) return BadRequest(new { message = "Only video items take a video file." });
        if (file.Length == 0) return BadRequest(new { message = "The file is empty." });
        if (file.Length > storageOptions.Value.MaxVideoBytes)
            return BadRequest(new { message = $"Videos can be at most {storageOptions.Value.MaxVideoBytes / 1024 / 1024} MB." });
        var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        if (!MediaController.VideoTypes.TryGetValue(ext, out var contentType))
            return BadRequest(new { message = "Upload an MP4, M4V, WebM or MOV file." });

        await using var stream = file.OpenReadStream();
        var key = await storage.SaveAsync(stream, "." + ext, "videos", ct);
        var oldKey = item.VideoFileKey;
        item.VideoFileKey = key;
        item.VideoFileName = Path.GetFileName(file.FileName);
        item.VideoContentType = contentType;
        item.VideoSizeBytes = file.Length;
        item.ExternalVideoUrl = null;
        item.DurationSeconds = null; // learned from the first player that loads the new file
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        if (oldKey is not null) await storage.DeleteAsync(oldKey);
        return await Get(id);
    }

    /// <summary>Uploads an image for use in an article and returns its URL.</summary>
    [HttpPost("images")]
    public async Task<ActionResult<UploadResult>> UploadImage(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0 || file.Length > storageOptions.Value.MaxImageBytes)
            return BadRequest(new { message = $"Images can be at most {storageOptions.Value.MaxImageBytes / 1024 / 1024} MB." });
        var ext = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        if (!MediaController.ImageTypes.ContainsKey(ext))
            return BadRequest(new { message = "Upload a PNG, JPG, GIF or WebP image." });
        await using var stream = file.OpenReadStream();
        var key = await storage.SaveAsync(stream, "." + ext, "images", ct);
        return new UploadResult($"/api/media/{key}");
    }

    private static void Apply(ContentItem item, ContentSaveRequest req)
    {
        item.ModuleId = req.ModuleId;
        item.Title = req.Title.Trim();
        item.Summary = string.IsNullOrWhiteSpace(req.Summary) ? null : req.Summary.Trim();
        item.Type = req.Type;
        item.SortOrder = req.SortOrder;
        item.IsPublished = req.IsPublished;
        item.EstimatedMinutes = req.EstimatedMinutes;
        if (req.Type == ContentType.Video)
        {
            item.ArticleBody = null;
            if (!string.IsNullOrWhiteSpace(req.ExternalVideoUrl))
            {
                item.ExternalVideoUrl = req.ExternalVideoUrl.Trim();
                item.DurationSeconds = req.DurationSeconds ?? item.DurationSeconds;
            }
            else
            {
                item.ExternalVideoUrl = null;
                if (req.DurationSeconds > 0) item.DurationSeconds = req.DurationSeconds;
            }
        }
        else
        {
            item.ArticleBody = req.ArticleBody ?? "";
            item.ExternalVideoUrl = null;
            item.DurationSeconds = null;
        }
        item.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<List<AdminContentDto>> ToDtos(ModuleCatalog catalog, List<ContentItem> items)
    {
        var ids = items.Select(i => i.Id).ToList();
        var stats = await db.ContentProgress.Where(p => ids.Contains(p.ContentItemId))
            .GroupBy(p => p.ContentItemId)
            .Select(g => new { g.Key, Viewers = g.Count(), Completed = g.Count(p => p.IsCompleted) })
            .ToDictionaryAsync(x => x.Key);
        return items.Select(c =>
        {
            stats.TryGetValue(c.Id, out var s);
            return new AdminContentDto(c.Id, c.ModuleId, catalog.Path(c.ModuleId), c.Title, c.Summary, c.Type,
                c.VideoFileName, c.VideoSizeBytes, c.ExternalVideoUrl, c.DurationSeconds, c.ArticleBody,
                c.EstimatedMinutes, c.SortOrder, c.IsPublished, c.CreatedAt, c.UpdatedAt, c.CreatedBy?.DisplayName,
                s?.Viewers ?? 0, s?.Completed ?? 0);
        }).ToList();
    }
}
