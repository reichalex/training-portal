using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrainingPortal.Api.Data;

namespace TrainingPortal.Api.Services;

public class ProgressOptions
{
    /// <summary>Share of a video that must be played for it to count as watched.</summary>
    public double CompletionThreshold { get; set; } = 0.9;
    /// <summary>Fastest playback speed the player offers; caps how far one heartbeat can advance.</summary>
    public double MaxPlaybackRate { get; set; } = 2.0;
    /// <summary>Allowance for clock and rounding differences between heartbeats.</summary>
    public int SlackSeconds { get; set; } = 5;
}

public record CourseStatus(int TotalItems, int CompletedItems, DateTime? EnrolledAt, DateTime? CompletedAt,
    DateTime? DueDate, bool Assigned)
{
    public int Percent => TotalItems == 0 ? 0 : (int)Math.Round(100.0 * CompletedItems / TotalItems);
}

public record ItemState(int ContentItemId, bool Completed, bool Locked);

public class ProgressService(PortalDbContext db, IOptions<ProgressOptions> options, TimeProvider clock)
{
    private readonly ProgressOptions _opt = options.Value;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Records a heartbeat from the video player. A learner only gets credit for the part
    /// of the video they actually played: the furthest position can advance by at most the
    /// time played since the last heartbeat (at the fastest playback rate), so skipping to
    /// the end does not complete a video.
    /// </summary>
    public async Task<ContentProgress> RecordVideoAsync(int userId, ContentItem item, int positionSeconds,
        int? reportedDurationSeconds, double playedSeconds)
    {
        var now = Now;
        var p = await db.ContentProgress.FirstOrDefaultAsync(x => x.UserId == userId && x.ContentItemId == item.Id);
        double elapsed = 0;
        if (p is null)
        {
            p = new ContentProgress { UserId = userId, ContentItemId = item.Id, FirstViewedAt = now, LastViewedAt = now };
            db.ContentProgress.Add(p);
        }
        else
        {
            elapsed = (now - p.LastViewedAt).TotalSeconds;
        }

        // Uploaded videos learn their length from the first player that loads them.
        if (item.DurationSeconds is null or 0 && reportedDurationSeconds > 0)
            item.DurationSeconds = reportedDurationSeconds;
        var duration = item.DurationSeconds ?? 0;

        var position = Math.Max(0, positionSeconds);
        if (duration > 0) position = Math.Min(position, duration);
        var played = Math.Clamp(playedSeconds, 0, elapsed);

        var allowedMax = p.MaxPositionSeconds + (int)Math.Ceiling(played * _opt.MaxPlaybackRate) + _opt.SlackSeconds;
        p.MaxPositionSeconds = Math.Max(p.MaxPositionSeconds, Math.Min(position, allowedMax));
        p.SecondsWatched += (int)Math.Round(played);
        p.LastPositionSeconds = position;
        p.LastViewedAt = now;

        var newlyCompleted = false;
        if (!p.IsCompleted && duration > 0 && p.MaxPositionSeconds >= duration * _opt.CompletionThreshold)
        {
            p.IsCompleted = true;
            p.CompletedAt = now;
            newlyCompleted = true;
        }

        await db.SaveChangesAsync();
        if (newlyCompleted) await UpdateCourseCompletionForContentAsync(userId, item.Id);
        return p;
    }

    /// <summary>Records that an article was opened, and optionally that it was read to the end.</summary>
    public async Task<ContentProgress> RecordArticleAsync(int userId, int contentId, bool completed)
    {
        var now = Now;
        var p = await db.ContentProgress.FirstOrDefaultAsync(x => x.UserId == userId && x.ContentItemId == contentId);
        if (p is null)
        {
            p = new ContentProgress { UserId = userId, ContentItemId = contentId, FirstViewedAt = now };
            db.ContentProgress.Add(p);
        }
        p.LastViewedAt = now;
        var newlyCompleted = completed && !p.IsCompleted;
        if (newlyCompleted)
        {
            p.IsCompleted = true;
            p.CompletedAt = now;
        }
        await db.SaveChangesAsync();
        if (newlyCompleted) await UpdateCourseCompletionForContentAsync(userId, contentId);
        return p;
    }

    public async Task<CourseEnrollment> EnrollAsync(int userId, int courseId, int? assignedById = null, DateTime? dueDate = null)
    {
        var e = await db.CourseEnrollments.FirstOrDefaultAsync(x => x.UserId == userId && x.CourseId == courseId);
        if (e is null)
        {
            e = new CourseEnrollment { UserId = userId, CourseId = courseId, EnrolledAt = Now };
            db.CourseEnrollments.Add(e);
        }
        if (assignedById is not null)
        {
            e.AssignedById = assignedById;
            e.DueDate = dueDate;
        }
        await db.SaveChangesAsync();
        // Someone may already have done every item before enrolling.
        await UpdateCourseCompletionAsync(userId, courseId);
        return e;
    }

    /// <summary>Progress of one user through each of the given courses (published items only).</summary>
    public async Task<Dictionary<int, CourseStatus>> CourseStatusesAsync(int userId, IReadOnlyCollection<int> courseIds)
    {
        var items = await db.CourseItems
            .Where(ci => courseIds.Contains(ci.CourseId) && ci.ContentItem.IsPublished)
            .Select(ci => new { ci.CourseId, ci.ContentItemId })
            .ToListAsync();
        var contentIds = items.Select(i => i.ContentItemId).Distinct().ToList();
        var completed = (await db.ContentProgress
            .Where(p => p.UserId == userId && p.IsCompleted && contentIds.Contains(p.ContentItemId))
            .Select(p => p.ContentItemId)
            .ToListAsync()).ToHashSet();
        var enrollments = await db.CourseEnrollments
            .Where(e => e.UserId == userId && courseIds.Contains(e.CourseId))
            .ToDictionaryAsync(e => e.CourseId);

        return courseIds.Distinct().ToDictionary(id => id, id =>
        {
            var ids = items.Where(i => i.CourseId == id).Select(i => i.ContentItemId).ToList();
            enrollments.TryGetValue(id, out var e);
            return new CourseStatus(ids.Count, ids.Count(completed.Contains), e?.EnrolledAt, e?.CompletedAt,
                e?.DueDate, e?.AssignedById is not null);
        });
    }

    /// <summary>Completed / locked state for each item of a course, in course order.</summary>
    public static List<ItemState> ItemStates(IEnumerable<int> orderedContentIds, ISet<int> completedIds, bool enforceOrder)
    {
        var states = new List<ItemState>();
        var allPreviousDone = true;
        foreach (var id in orderedContentIds)
        {
            var done = completedIds.Contains(id);
            states.Add(new ItemState(id, done, enforceOrder && !allPreviousDone));
            allPreviousDone &= done;
        }
        return states;
    }

    /// <summary>
    /// Returns the published content ids of a course in order, or null if the course does not
    /// exist or is not published.
    /// </summary>
    public async Task<List<int>?> PublishedCourseSequenceAsync(int courseId)
    {
        var course = await db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.Id == courseId && c.IsPublished);
        if (course is null) return null;
        return await db.CourseItems
            .Where(ci => ci.CourseId == courseId && ci.ContentItem.IsPublished)
            .OrderBy(ci => ci.Position)
            .Select(ci => ci.ContentItemId)
            .ToListAsync();
    }

    public async Task<HashSet<int>> CompletedContentIdsAsync(int userId, IEnumerable<int> contentIds)
    {
        var ids = contentIds.ToList();
        return (await db.ContentProgress
            .Where(p => p.UserId == userId && p.IsCompleted && ids.Contains(p.ContentItemId))
            .Select(p => p.ContentItemId)
            .ToListAsync()).ToHashSet();
    }

    private async Task UpdateCourseCompletionForContentAsync(int userId, int contentId)
    {
        var courseIds = await db.CourseEnrollments
            .Where(e => e.UserId == userId && e.CompletedAt == null
                        && db.CourseItems.Any(ci => ci.CourseId == e.CourseId && ci.ContentItemId == contentId))
            .Select(e => e.CourseId)
            .ToListAsync();
        foreach (var courseId in courseIds)
            await UpdateCourseCompletionAsync(userId, courseId);
    }

    public async Task UpdateCourseCompletionAsync(int userId, int courseId)
    {
        var enrollment = await db.CourseEnrollments.FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == courseId);
        if (enrollment is null || enrollment.CompletedAt is not null) return;
        var status = (await CourseStatusesAsync(userId, [courseId]))[courseId];
        if (status.TotalItems > 0 && status.CompletedItems == status.TotalItems)
        {
            enrollment.CompletedAt = Now;
            await db.SaveChangesAsync();
        }
    }
}
