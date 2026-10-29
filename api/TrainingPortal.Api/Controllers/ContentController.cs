using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Dtos;
using TrainingPortal.Api.Services;

namespace TrainingPortal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/content")]
public class ContentController(PortalDbContext db, ProgressService progress, MediaLinkService media) : ControllerBase
{
    /// <summary>
    /// One video or article. With <paramref name="courseId"/> it is opened as a step of that
    /// course: the user is enrolled, and a step whose earlier steps are unfinished is refused.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ContentDetail>> Get(int id, [FromQuery] int? courseId)
    {
        var item = await db.ContentItems.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (item is null) return NotFound();
        var catalog = await ModuleCatalog.LoadAsync(db);
        var isAdmin = User.IsInRole(nameof(UserRole.Admin));
        var userId = User.UserId();

        CourseContext? courseContext = null;
        if (courseId is int cid)
        {
            var sequence = await progress.PublishedCourseSequenceAsync(cid);
            if (sequence is null || !sequence.Contains(id)) return NotFound();
            var course = await db.Courses.AsNoTracking().FirstAsync(c => c.Id == cid);
            var completed = await progress.CompletedContentIdsAsync(userId, sequence);
            var states = ProgressService.ItemStates(sequence, completed, course.EnforceOrder);
            var index = sequence.IndexOf(id);
            if (states[index].Locked && !isAdmin)
                return Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Finish the earlier steps of this course first.");

            var enrollment = await progress.EnrollAsync(userId, cid);
            courseContext = new CourseContext(cid, course.Title, index + 1, sequence.Count,
                index > 0 ? sequence[index - 1] : null,
                index < sequence.Count - 1 ? sequence[index + 1] : null,
                course.EnforceOrder, enrollment.CompletedAt is not null);
        }
        else if (!isAdmin && !await Summaries.IsVisibleAsync(db, catalog, item))
        {
            return NotFound();
        }

        var p = await db.ContentProgress.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ContentItemId == id);
        var videoUrl = item.Type != ContentType.Video ? null
            : item.VideoFileKey is not null ? media.VideoUrl(item.Id, userId)
            : item.ExternalVideoUrl;

        return new ContentDetail(item.Id, item.ModuleId, catalog.Breadcrumb(item.ModuleId), item.Title, item.Summary,
            item.Type, videoUrl, item.DurationSeconds,
            item.Type == ContentType.Article ? item.ArticleBody : null,
            item.EstimatedMinutes, item.UpdatedAt, p is null ? null : ProgressDto.From(p), courseContext);
    }

    /// <summary>Searches titles and summaries of everything the user can open.</summary>
    [HttpGet("search")]
    public async Task<List<ContentSummary>> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return [];
        var term = q.Trim();
        var catalog = await ModuleCatalog.LoadAsync(db);
        var items = await db.ContentItems.AsNoTracking()
            .Where(c => c.IsPublished && (c.Title.Contains(term) || (c.Summary != null && c.Summary.Contains(term))))
            .OrderBy(c => c.Title)
            .Take(100)
            .ToListAsync();
        items = items.Where(c => catalog.IsVisible(c.ModuleId)).Take(50).ToList();
        return await Summaries.ContentAsync(db, User.UserId(), items);
    }
}
