using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Dtos;
using TrainingPortal.Api.Services;

namespace TrainingPortal.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/modules")]
public class AdminModulesController(PortalDbContext db) : ControllerBase
{
    /// <summary>Every module, published or not, in tree order (parents before their children).</summary>
    [HttpGet]
    public async Task<List<AdminModuleDto>> List()
    {
        var catalog = await ModuleCatalog.LoadAsync(db);
        var contentCounts = await db.ContentItems.GroupBy(c => c.ModuleId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

        var result = new List<AdminModuleDto>();
        void Walk(int? parentId)
        {
            foreach (var m in catalog.ChildrenOf(parentId))
            {
                result.Add(new AdminModuleDto(m.Id, m.ParentId, m.Name, m.Description, m.SortOrder, m.IsPublished,
                    contentCounts.GetValueOrDefault(m.Id), catalog.ChildrenOf(m.Id).Count(), catalog.Path(m.Id)));
                Walk(m.Id);
            }
        }
        Walk(null);
        return result;
    }

    [HttpPost]
    public async Task<ActionResult<AdminModuleDto>> Create(ModuleSaveRequest req)
    {
        if (req.ParentId is int p && !await db.Modules.AnyAsync(m => m.Id == p))
            return BadRequest(new { message = "Parent module not found." });
        var module = new TrainingModule();
        Apply(module, req);
        db.Modules.Add(module);
        await db.SaveChangesAsync();
        return await Single(module.Id);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<AdminModuleDto>> Update(int id, ModuleSaveRequest req)
    {
        var module = await db.Modules.FindAsync(id);
        if (module is null) return NotFound();
        var catalog = await ModuleCatalog.LoadAsync(db);
        if (req.ParentId is int p && catalog.Get(p) is null)
            return BadRequest(new { message = "Parent module not found." });
        if (catalog.WouldCreateCycle(id, req.ParentId))
            return BadRequest(new { message = "A module cannot be moved inside itself." });
        Apply(module, req);
        await db.SaveChangesAsync();
        return await Single(id);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var module = await db.Modules.FindAsync(id);
        if (module is null) return NotFound();
        if (await db.Modules.AnyAsync(m => m.ParentId == id)
            || await db.ContentItems.AnyAsync(c => c.ModuleId == id)
            || await db.Courses.AnyAsync(c => c.ModuleId == id))
            return Conflict(new { message = "Move or delete this module's sections, content and courses first." });
        db.Modules.Remove(module);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(TrainingModule m, ModuleSaveRequest req)
    {
        m.ParentId = req.ParentId;
        m.Name = req.Name.Trim();
        m.Description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        m.SortOrder = req.SortOrder;
        m.IsPublished = req.IsPublished;
    }

    private async Task<AdminModuleDto> Single(int id) => (await List()).First(m => m.Id == id);
}
