using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Dtos;
using TrainingPortal.Api.Services;

namespace TrainingPortal.Api.Controllers.Admin;

/// <summary>Who has watched or read what, and how far everyone is through their courses.</summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/reports")]
public class AdminReportsController(PortalDbContext db, ProgressService progress) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<OverviewReport> Overview()
    {
        var since = DateTime.UtcNow.AddDays(-30);
        var top = await db.ContentProgress
            .GroupBy(p => p.ContentItemId)
            .Select(g => new { ContentId = g.Key, Viewers = g.Count(), Completions = g.Count(p => p.IsCompleted) })
            .OrderByDescending(x => x.Viewers)
            .Take(10)
            .ToListAsync();
        var topIds = top.Select(t => t.ContentId).ToList();
        var titles = await db.ContentItems.Where(c => topIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new { c.Title, c.Type });

        return new OverviewReport(
            await db.Users.CountAsync(u => u.IsActive),
            await db.Modules.CountAsync(),
            await db.ContentItems.CountAsync(c => c.Type == ContentType.Video),
            await db.ContentItems.CountAsync(c => c.Type == ContentType.Article),
            await db.Courses.CountAsync(),
            await db.ContentProgress.CountAsync(p => p.CompletedAt >= since),
            await db.ContentProgress.Where(p => p.LastViewedAt >= since).Select(p => p.UserId).Distinct().CountAsync(),
            top.Where(t => titles.ContainsKey(t.ContentId)).Select(t => new TopContentRow(t.ContentId,
                titles[t.ContentId].Title, titles[t.ContentId].Type, t.Viewers, t.Completions)).ToList());
    }

    [HttpGet("users")]
    public async Task<List<UserReportRow>> Users()
    {
        var now = DateTime.UtcNow;
        var content = await db.ContentProgress.GroupBy(p => p.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Started = g.Count(),
                Completed = g.Count(p => p.IsCompleted),
                Seconds = g.Sum(p => (long)p.SecondsWatched),
                Last = g.Max(p => p.LastViewedAt),
            })
            .ToDictionaryAsync(x => x.UserId);
        var courses = await db.CourseEnrollments.GroupBy(e => e.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                Enrolled = g.Count(),
                Completed = g.Count(e => e.CompletedAt != null),
                Overdue = g.Count(e => e.CompletedAt == null && e.DueDate < now),
            })
            .ToDictionaryAsync(x => x.UserId);
        var users = await db.Users.AsNoTracking().OrderBy(u => u.DisplayName).ToListAsync();

        return users.Select(u =>
        {
            content.TryGetValue(u.Id, out var c);
            courses.TryGetValue(u.Id, out var e);
            return new UserReportRow(u.Id, u.DisplayName, u.Email, u.Department, c?.Started ?? 0, c?.Completed ?? 0,
                e?.Enrolled ?? 0, e?.Completed ?? 0, e?.Overdue ?? 0, (int)((c?.Seconds ?? 0) / 60), c?.Last);
        }).ToList();
    }

    [HttpGet("users/{id:int}")]
    public async Task<ActionResult<UserReportDetail>> UserDetail(int id)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        var catalog = await ModuleCatalog.LoadAsync(db);
        var rows = await db.ContentProgress.AsNoTracking().Include(p => p.ContentItem)
            .Where(p => p.UserId == id)
            .OrderByDescending(p => p.LastViewedAt)
            .ToListAsync();
        var courses = await db.CourseEnrollments.AsNoTracking().Where(e => e.UserId == id).Select(e => e.Course)
            .OrderBy(c => c.Title).ToListAsync();

        return new UserReportDetail(UserDto.From(user),
            rows.Select(p => new UserContentRow(p.ContentItemId, p.ContentItem.Title, catalog.Path(p.ContentItem.ModuleId),
                p.ContentItem.Type, p.IsCompleted, Summaries.Percent(p.ContentItem, p), p.SecondsWatched,
                p.FirstViewedAt, p.LastViewedAt, p.CompletedAt)).ToList(),
            await Summaries.CoursesAsync(db, progress, catalog, id, courses));
    }

    [HttpGet("content/{id:int}")]
    public async Task<ActionResult<ContentReport>> Content(int id)
    {
        var item = await db.ContentItems.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (item is null) return NotFound();
        var rows = await db.ContentProgress.AsNoTracking().Include(p => p.User)
            .Where(p => p.ContentItemId == id)
            .OrderBy(p => p.User.DisplayName)
            .ToListAsync();
        return new ContentReport(item.Id, item.Title, item.Type, rows.Select(p => new ContentViewerRow(p.UserId,
            p.User.DisplayName, p.User.Email, p.User.Department, Summaries.Percent(item, p), p.IsCompleted,
            p.SecondsWatched, p.FirstViewedAt, p.LastViewedAt, p.CompletedAt)).ToList());
    }

    [HttpGet("courses/{id:int}")]
    public async Task<ActionResult<CourseReport>> Course(int id)
    {
        var course = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (course is null) return NotFound();
        return new CourseReport(course.Id, course.Title, await CourseRows(id));
    }

    /// <summary>Every content view as CSV: one row per person per video or article.</summary>
    [HttpGet("export/content.csv")]
    public async Task<IActionResult> ExportContent()
    {
        var catalog = await ModuleCatalog.LoadAsync(db);
        var rows = await db.ContentProgress.AsNoTracking().Include(p => p.User).Include(p => p.ContentItem)
            .OrderBy(p => p.User.DisplayName).ThenBy(p => p.FirstViewedAt)
            .ToListAsync();
        var csv = new StringBuilder();
        Line(csv, "Name", "Email", "Department", "Module", "Content", "Type", "Percent", "Completed",
            "Completed at (UTC)", "First viewed (UTC)", "Last viewed (UTC)", "Minutes watched");
        foreach (var p in rows)
            Line(csv, p.User.DisplayName, p.User.Email, p.User.Department, catalog.Path(p.ContentItem.ModuleId),
                p.ContentItem.Title, p.ContentItem.Type.ToString(), Summaries.Percent(p.ContentItem, p).ToString(),
                p.IsCompleted ? "Yes" : "No", Date(p.CompletedAt), Date(p.FirstViewedAt), Date(p.LastViewedAt),
                (p.SecondsWatched / 60.0).ToString("0.#", CultureInfo.InvariantCulture));
        return Csv(csv, "content-progress");
    }

    /// <summary>Every course enrollment as CSV.</summary>
    [HttpGet("export/courses.csv")]
    public async Task<IActionResult> ExportCourses()
    {
        var courses = await db.Courses.AsNoTracking().OrderBy(c => c.Title).ToListAsync();
        var csv = new StringBuilder();
        Line(csv, "Course", "Name", "Email", "Department", "Assigned", "Due (UTC)", "Overdue", "Steps done",
            "Steps total", "Percent", "Enrolled (UTC)", "Completed (UTC)");
        foreach (var course in courses)
        foreach (var r in await CourseRows(course.Id))
            Line(csv, course.Title, r.DisplayName, r.Email, r.Department, r.Assigned ? "Yes" : "No", Date(r.DueDate),
                r.Overdue ? "Yes" : "No", r.CompletedItems.ToString(), r.TotalItems.ToString(), r.Percent.ToString(),
                Date(r.EnrolledAt), Date(r.CompletedAt));
        return Csv(csv, "course-progress");
    }

    private async Task<List<CourseLearnerRow>> CourseRows(int courseId)
    {
        var now = DateTime.UtcNow;
        var enrollments = await db.CourseEnrollments.AsNoTracking().Include(e => e.User)
            .Where(e => e.CourseId == courseId)
            .OrderBy(e => e.User.DisplayName)
            .ToListAsync();
        var itemIds = await db.CourseItems.Where(ci => ci.CourseId == courseId && ci.ContentItem.IsPublished)
            .Select(ci => ci.ContentItemId).ToListAsync();
        var userIds = enrollments.Select(e => e.UserId).ToList();
        var done = await db.ContentProgress
            .Where(p => p.IsCompleted && userIds.Contains(p.UserId) && itemIds.Contains(p.ContentItemId))
            .GroupBy(p => p.UserId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return enrollments.Select(e =>
        {
            var completed = e.CompletedAt is not null ? itemIds.Count : done.GetValueOrDefault(e.UserId);
            var percent = itemIds.Count == 0 ? 0 : (int)Math.Round(100.0 * completed / itemIds.Count);
            return new CourseLearnerRow(e.UserId, e.User.DisplayName, e.User.Email, e.User.Department, completed,
                itemIds.Count, percent, e.EnrolledAt, e.CompletedAt, e.DueDate, e.AssignedById is not null,
                e.CompletedAt is null && e.DueDate < now);
        }).ToList();
    }

    private static string Date(DateTime? d) => d?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "";

    private static void Line(StringBuilder sb, params string?[] fields)
    {
        sb.AppendJoin(',', fields.Select(Field)).Append("\r\n");
    }

    private static string Field(string? value)
    {
        value ??= "";
        // Stop spreadsheet apps from treating a cell as a formula.
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    private FileContentResult Csv(StringBuilder csv, string name) =>
        File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv", $"{name}-{DateTime.UtcNow:yyyyMMdd}.csv");
}
