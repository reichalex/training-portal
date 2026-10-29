using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Dtos;
using TrainingPortal.Api.Services;

namespace TrainingPortal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/me")]
public class MeController(PortalDbContext db, ProgressService progress) : ControllerBase
{
    /// <summary>The learner's home page: assigned courses, what to pick up again, and totals.</summary>
    [HttpGet("dashboard")]
    public async Task<Dashboard> Dashboard()
    {
        var userId = User.UserId();
        var catalog = await ModuleCatalog.LoadAsync(db);

        var enrolledCourses = await db.CourseEnrollments.AsNoTracking()
            .Where(e => e.UserId == userId && e.Course.IsPublished)
            .Select(e => e.Course)
            .ToListAsync();
        var courses = await Summaries.CoursesAsync(db, progress, catalog, userId, enrolledCourses);
        var open = courses.Where(c => c.CompletedAt is null).ToList();

        var recent = await db.ContentProgress.AsNoTracking()
            .Where(p => p.UserId == userId && p.ContentItem.IsPublished)
            .OrderByDescending(p => p.LastViewedAt)
            .Include(p => p.ContentItem)
            .Take(50)
            .ToListAsync();
        var continueWatching = recent.Where(p => !p.IsCompleted).Take(6)
            .Select(p => Summaries.Content(p.ContentItem, p)).ToList();
        var recentlyCompleted = recent.Where(p => p.IsCompleted).OrderByDescending(p => p.CompletedAt).Take(6)
            .Select(p => Summaries.Content(p.ContentItem, p)).ToList();

        var completedItems = await db.ContentProgress.CountAsync(p => p.UserId == userId && p.IsCompleted);
        var seconds = await db.ContentProgress.Where(p => p.UserId == userId).SumAsync(p => (long)p.SecondsWatched);

        return new Dashboard(
            open.Where(c => c.Assigned).OrderBy(c => c.DueDate ?? DateTime.MaxValue).ToList(),
            open.Where(c => !c.Assigned).OrderByDescending(c => c.EnrolledAt).ToList(),
            continueWatching,
            recentlyCompleted,
            new DashboardStats(completedItems, courses.Count(c => c.CompletedAt is not null), (int)(seconds / 60)));
    }
}
