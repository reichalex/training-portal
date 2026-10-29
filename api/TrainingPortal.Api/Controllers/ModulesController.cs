using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Dtos;
using TrainingPortal.Api.Services;

namespace TrainingPortal.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/modules")]
public class ModulesController(PortalDbContext db, ProgressService progress) : ControllerBase
{
    /// <summary>The published module tree with how much of each the current user has completed.</summary>
    [HttpGet("tree")]
    public async Task<List<ModuleTreeNode>> Tree()
    {
        var catalog = await ModuleCatalog.LoadAsync(db);
        var counter = await CounterAsync(catalog);
        return catalog.ChildrenOf(null).Where(m => m.IsPublished).Select(m => Node(catalog, m, counter)).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ModuleDetail>> Get(int id)
    {
        var catalog = await ModuleCatalog.LoadAsync(db);
        var module = catalog.Get(id);
        if (module is null || !catalog.IsVisible(id)) return NotFound();

        var counter = await CounterAsync(catalog);
        var children = catalog.ChildrenOf(id).Where(m => m.IsPublished).Select(m => Node(catalog, m, counter)).ToList();

        var items = await db.ContentItems.AsNoTracking()
            .Where(c => c.ModuleId == id && c.IsPublished)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Title)
            .ToListAsync();
        var courses = await db.Courses.AsNoTracking()
            .Where(c => c.ModuleId == id && c.IsPublished)
            .OrderBy(c => c.Title)
            .ToListAsync();

        var userId = User.UserId();
        return new ModuleDetail(module.Id, module.Name, module.Description, catalog.Breadcrumb(id), children,
            await Summaries.ContentAsync(db, userId, items),
            await Summaries.CoursesAsync(db, progress, catalog, userId, courses));
    }

    private record Counter(ILookup<int, int> ContentByModule, HashSet<int> Completed);

    private async Task<Counter> CounterAsync(ModuleCatalog catalog)
    {
        var published = await db.ContentItems.Where(c => c.IsPublished)
            .Select(c => new { c.Id, c.ModuleId }).ToListAsync();
        var userId = User.UserId();
        var completed = (await db.ContentProgress.Where(p => p.UserId == userId && p.IsCompleted)
            .Select(p => p.ContentItemId).ToListAsync()).ToHashSet();
        return new Counter(published.ToLookup(c => c.ModuleId, c => c.Id), completed);
    }

    private static ModuleTreeNode Node(ModuleCatalog catalog, TrainingModule m, Counter counter)
    {
        var children = catalog.ChildrenOf(m.Id).Where(c => c.IsPublished)
            .Select(c => Node(catalog, c, counter)).ToList();
        var own = counter.ContentByModule[m.Id].ToList();
        return new ModuleTreeNode(m.Id, m.Name, m.Description,
            own.Count + children.Sum(c => c.ContentCount),
            own.Count(counter.Completed.Contains) + children.Sum(c => c.CompletedCount),
            children);
    }
}
