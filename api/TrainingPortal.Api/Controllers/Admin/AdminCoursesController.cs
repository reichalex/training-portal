using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Dtos;
using TrainingPortal.Api.Services;

namespace TrainingPortal.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/courses")]
public class AdminCoursesController(PortalDbContext db, ProgressService progress) : ControllerBase
{
    [HttpGet]
    public async Task<List<AdminCourseDto>> List()
    {
        var courses = await db.Courses.AsNoTracking().OrderBy(c => c.Title).ToListAsync();
        return await ToDtos(courses);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminCourseDto>> Get(int id)
    {
        var course = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return course is null ? NotFound() : (await ToDtos([course]))[0];
    }

    [HttpPost]
    public async Task<ActionResult<AdminCourseDto>> Create(CourseSaveRequest req)
    {
        var error = await Validate(req);
        if (error is not null) return BadRequest(new { message = error });
        var course = new Course();
        Apply(course, req);
        db.Courses.Add(course);
        await db.SaveChangesAsync();
        return await Get(course.Id);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AdminCourseDto>> Update(int id, CourseSaveRequest req)
    {
        var course = await db.Courses.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == id);
        if (course is null) return NotFound();
        var error = await Validate(req);
        if (error is not null) return BadRequest(new { message = error });
        db.CourseItems.RemoveRange(course.Items);
        await db.SaveChangesAsync(); // clear old rows first so the unique (course, content) index never clashes
        Apply(course, req);
        await db.SaveChangesAsync();

        // Completed courses stay completed when steps are added, but removing a step can
        // complete the course for people who had already done all the others.
        var learners = await db.CourseEnrollments.Where(e => e.CourseId == id && e.CompletedAt == null)
            .Select(e => e.UserId).ToListAsync();
        foreach (var userId in learners) await progress.UpdateCourseCompletionAsync(userId, id);
        return await Get(id);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var course = await db.Courses.FindAsync(id);
        if (course is null) return NotFound();
        db.Courses.Remove(course);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Assigns the course to people, optionally with a due date.</summary>
    [HttpPost("{id:int}/assign")]
    public async Task<IActionResult> Assign(int id, AssignRequest req)
    {
        if (!await db.Courses.AnyAsync(c => c.Id == id)) return NotFound();
        var userIds = await db.Users.Where(u => req.UserIds.Contains(u.Id)).Select(u => u.Id).ToListAsync();
        var adminId = User.UserId();
        foreach (var userId in userIds)
            await progress.EnrollAsync(userId, id, adminId, req.DueDate);
        return NoContent();
    }

    /// <summary>Withdraws an assignment. The learner's viewing history is kept.</summary>
    [HttpDelete("{id:int}/assign/{userId:int}")]
    public async Task<IActionResult> Unassign(int id, int userId)
    {
        var e = await db.CourseEnrollments.FirstOrDefaultAsync(x => x.CourseId == id && x.UserId == userId);
        if (e is null) return NotFound();
        db.CourseEnrollments.Remove(e);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<string?> Validate(CourseSaveRequest req)
    {
        if (req.ModuleId is int m && !await db.Modules.AnyAsync(x => x.Id == m)) return "Module not found.";
        if (req.ContentIds.Distinct().Count() != req.ContentIds.Count) return "An item can appear only once in a course.";
        var found = await db.ContentItems.CountAsync(c => req.ContentIds.Contains(c.Id));
        return found != req.ContentIds.Count ? "Some of the selected content no longer exists." : null;
    }

    private static void Apply(Course c, CourseSaveRequest req)
    {
        c.Title = req.Title.Trim();
        c.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        c.ModuleId = req.ModuleId;
        c.EnforceOrder = req.EnforceOrder;
        c.IsPublished = req.IsPublished;
        c.Items = req.ContentIds.Select((contentId, i) => new CourseItem { ContentItemId = contentId, Position = i + 1 })
            .ToList();
    }

    private async Task<List<AdminCourseDto>> ToDtos(List<Course> courses)
    {
        var catalog = await ModuleCatalog.LoadAsync(db);
        var ids = courses.Select(c => c.Id).ToList();
        var items = await db.CourseItems.AsNoTracking().Where(ci => ids.Contains(ci.CourseId))
            .OrderBy(ci => ci.Position)
            .Select(ci => new { ci.CourseId, ci.ContentItemId, ci.ContentItem.Title, ci.ContentItem.Type,
                ci.ContentItem.IsPublished, ci.ContentItem.ModuleId })
            .ToListAsync();
        var stats = await db.CourseEnrollments.Where(e => ids.Contains(e.CourseId))
            .GroupBy(e => e.CourseId)
            .Select(g => new { g.Key, Enrolled = g.Count(), Completed = g.Count(e => e.CompletedAt != null) })
            .ToDictionaryAsync(x => x.Key);
        return courses.Select(c =>
        {
            stats.TryGetValue(c.Id, out var s);
            return new AdminCourseDto(c.Id, c.Title, c.Description, c.ModuleId, c.EnforceOrder, c.IsPublished,
                c.CreatedAt,
                items.Where(i => i.CourseId == c.Id).Select(i => new AdminCourseItem(i.ContentItemId, i.Title, i.Type,
                    i.IsPublished, catalog.Path(i.ModuleId))).ToList(),
                s?.Enrolled ?? 0, s?.Completed ?? 0);
        }).ToList();
    }
}
