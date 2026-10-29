using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Dtos;
using TrainingPortal.Api.Services;

namespace TrainingPortal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/courses")]
public class CoursesController(PortalDbContext db, ProgressService progress) : ControllerBase
{
    [HttpGet]
    public async Task<List<CourseSummary>> List()
    {
        var catalog = await ModuleCatalog.LoadAsync(db);
        var courses = await db.Courses.AsNoTracking().Where(c => c.IsPublished).OrderBy(c => c.Title).ToListAsync();
        return await Summaries.CoursesAsync(db, progress, catalog, User.UserId(), courses);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CourseDetail>> Get(int id)
    {
        var course = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.IsPublished);
        if (course is null) return NotFound();
        var catalog = await ModuleCatalog.LoadAsync(db);
        var userId = User.UserId();

        var items = await db.CourseItems.AsNoTracking()
            .Where(ci => ci.CourseId == id && ci.ContentItem.IsPublished)
            .OrderBy(ci => ci.Position)
            .Select(ci => ci.ContentItem)
            .ToListAsync();
        var completed = await progress.CompletedContentIdsAsync(userId, items.Select(i => i.Id));
        var states = ProgressService.ItemStates(items.Select(i => i.Id), completed, course.EnforceOrder);
        var summary = (await Summaries.CoursesAsync(db, progress, catalog, userId, [course]))[0];

        return new CourseDetail(summary, course.EnforceOrder, items.Select((c, i) => new CourseItemDto(
            i + 1, c.Id, c.Title, c.Type, c.DurationSeconds, c.EstimatedMinutes,
            states[i].Completed, states[i].Locked)).ToList());
    }

    [HttpPost("{id:int}/enroll")]
    public async Task<IActionResult> Enroll(int id)
    {
        if (!await db.Courses.AnyAsync(c => c.Id == id && c.IsPublished)) return NotFound();
        await progress.EnrollAsync(User.UserId(), id);
        return NoContent();
    }
}
