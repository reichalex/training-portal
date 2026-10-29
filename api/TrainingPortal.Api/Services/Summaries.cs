using Microsoft.EntityFrameworkCore;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Dtos;

namespace TrainingPortal.Api.Services;

/// <summary>Builds the learner-facing summaries shared by several endpoints.</summary>
public static class Summaries
{
    public static int Percent(ContentItem c, ContentProgress? p)
    {
        if (p is null) return 0;
        if (p.IsCompleted) return 100;
        if (c.Type == ContentType.Article || c.DurationSeconds is null or 0) return 0;
        return Math.Min(99, (int)(100.0 * p.MaxPositionSeconds / c.DurationSeconds.Value));
    }

    public static ContentSummary Content(ContentItem c, ContentProgress? p) =>
        new(c.Id, c.ModuleId, c.Title, c.Summary, c.Type, c.DurationSeconds, c.EstimatedMinutes,
            p?.IsCompleted ?? false, Percent(c, p), p?.LastViewedAt);

    public static async Task<List<ContentSummary>> ContentAsync(PortalDbContext db, int userId, List<ContentItem> items)
    {
        var ids = items.Select(c => c.Id).ToList();
        var progress = await db.ContentProgress
            .Where(p => p.UserId == userId && ids.Contains(p.ContentItemId))
            .ToDictionaryAsync(p => p.ContentItemId);
        return items.Select(c => Content(c, progress.GetValueOrDefault(c.Id))).ToList();
    }

    public static async Task<List<CourseSummary>> CoursesAsync(PortalDbContext db, ProgressService progress,
        ModuleCatalog catalog, int userId, List<Course> courses)
    {
        var statuses = await progress.CourseStatusesAsync(userId, courses.Select(c => c.Id).ToList());
        return courses.Select(c =>
        {
            var s = statuses[c.Id];
            return new CourseSummary(c.Id, c.Title, c.Description, c.ModuleId,
                c.ModuleId is int m ? catalog.Get(m)?.Name : null,
                s.TotalItems, s.CompletedItems, s.Percent, s.EnrolledAt, s.CompletedAt, s.DueDate, s.Assigned);
        }).ToList();
    }

    /// <summary>
    /// Whether a learner may open a content item: it is published, and either its module
    /// (with every parent) is published or it belongs to a published course. Putting items in
    /// an unpublished module therefore makes them reachable only through courses.
    /// </summary>
    public static async Task<bool> IsVisibleAsync(PortalDbContext db, ModuleCatalog catalog, ContentItem c) =>
        c.IsPublished && (catalog.IsVisible(c.ModuleId)
                          || await db.CourseItems.AnyAsync(ci => ci.ContentItemId == c.Id && ci.Course.IsPublished));
}
