using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Dtos;
using TrainingPortal.Api.Services;

namespace TrainingPortal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/progress")]
public class ProgressController(PortalDbContext db, ProgressService progress) : ControllerBase
{
    /// <summary>Sent by the video player every few seconds while playing, and on pause and end.</summary>
    [HttpPost("{contentId:int}/heartbeat")]
    public async Task<ActionResult<ProgressDto>> Heartbeat(int contentId, HeartbeatRequest req)
    {
        var item = await db.ContentItems.FindAsync(contentId);
        if (item is null || item.Type != ContentType.Video || !await CanOpenAsync(item)) return NotFound();
        var p = await progress.RecordVideoAsync(User.UserId(), item, req.PositionSeconds, req.DurationSeconds,
            req.PlayedSeconds);
        return ProgressDto.From(p);
    }

    /// <summary>Records that an article was opened (completed=false) or read (completed=true).</summary>
    [HttpPost("{contentId:int}/article")]
    public async Task<ActionResult<ProgressDto>> Article(int contentId, [FromQuery] bool completed)
    {
        var item = await db.ContentItems.FindAsync(contentId);
        if (item is null || item.Type != ContentType.Article || !await CanOpenAsync(item)) return NotFound();
        return ProgressDto.From(await progress.RecordArticleAsync(User.UserId(), contentId, completed));
    }

    private async Task<bool> CanOpenAsync(ContentItem item) =>
        User.IsInRole(nameof(UserRole.Admin))
        || await Summaries.IsVisibleAsync(db, await ModuleCatalog.LoadAsync(db), item);
}
