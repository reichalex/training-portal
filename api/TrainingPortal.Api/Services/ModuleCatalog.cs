using Microsoft.EntityFrameworkCore;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Dtos;

namespace TrainingPortal.Api.Services;

/// <summary>
/// The whole module tree held in memory for one request. Training catalogs have tens or
/// hundreds of modules, so loading them all is cheaper than walking the tree in SQL.
/// </summary>
public class ModuleCatalog
{
    private readonly Dictionary<int, TrainingModule> _byId;

    private ModuleCatalog(List<TrainingModule> modules)
    {
        _byId = modules.ToDictionary(m => m.Id);
    }

    public static async Task<ModuleCatalog> LoadAsync(PortalDbContext db) =>
        new(await db.Modules.AsNoTracking().ToListAsync());

    public IEnumerable<TrainingModule> All => _byId.Values;

    public TrainingModule? Get(int id) => _byId.GetValueOrDefault(id);

    public IEnumerable<TrainingModule> ChildrenOf(int? parentId) =>
        _byId.Values.Where(m => m.ParentId == parentId).OrderBy(m => m.SortOrder).ThenBy(m => m.Name);

    /// <summary>Root first, ending with the module itself.</summary>
    public List<Crumb> Breadcrumb(int id)
    {
        var crumbs = new List<Crumb>();
        for (var m = Get(id); m is not null; m = m.ParentId is int p ? Get(p) : null)
        {
            crumbs.Insert(0, new Crumb(m.Id, m.Name));
            if (crumbs.Count > 50) break; // guards against a cycle in bad data
        }
        return crumbs;
    }

    public string Path(int id) => string.Join(" / ", Breadcrumb(id).Select(c => c.Name));

    /// <summary>Visible to learners: the module and every ancestor are published.</summary>
    public bool IsVisible(int id)
    {
        var depth = 0;
        for (var m = Get(id); m is not null; m = m.ParentId is int p ? Get(p) : null)
        {
            if (!m.IsPublished || ++depth > 50) return false;
        }
        return depth > 0;
    }

    /// <summary>The module and all modules beneath it.</summary>
    public HashSet<int> Subtree(int id)
    {
        var result = new HashSet<int>();
        var stack = new Stack<int>([id]);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!result.Add(current)) continue;
            foreach (var child in _byId.Values.Where(m => m.ParentId == current)) stack.Push(child.Id);
        }
        return result;
    }

    /// <summary>True if making <paramref name="newParentId"/> the parent of <paramref name="id"/> would create a loop.</summary>
    public bool WouldCreateCycle(int id, int? newParentId) =>
        newParentId is int p && Subtree(id).Contains(p);
}
