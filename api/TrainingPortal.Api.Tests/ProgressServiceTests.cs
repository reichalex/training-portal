using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Services;

namespace TrainingPortal.Api.Tests;

public sealed class ManualClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(double seconds) => Now = Now.AddSeconds(seconds);
}

public sealed class ProgressServiceTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly PortalDbContext _db;
    private readonly ManualClock _clock = new();
    private readonly ProgressService _svc;
    private readonly User _user;
    private readonly TrainingModule _module;

    public ProgressServiceTests()
    {
        _conn.Open();
        _db = new PortalDbContext(new DbContextOptionsBuilder<PortalDbContext>().UseSqlite(_conn).Options);
        _db.Database.EnsureCreated();
        _svc = new ProgressService(_db, Options.Create(new ProgressOptions()), _clock);
        _user = new User { Email = "a@b.c", DisplayName = "A" };
        _module = new TrainingModule { Name = "Power BI" };
        _db.AddRange(_user, _module);
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _conn.Dispose();
    }

    private ContentItem AddItem(ContentType type, int? duration = null)
    {
        var item = new ContentItem
        {
            Module = _module, Title = $"Item {Guid.NewGuid():N}", Type = type, DurationSeconds = duration,
            IsPublished = true,
        };
        _db.ContentItems.Add(item);
        _db.SaveChanges();
        return item;
    }

    /// <summary>Plays from the current max to <paramref name="until"/>, heartbeating every 10 s.</summary>
    private async Task<ContentProgress> Play(ContentItem item, int from, int until, int? duration = null)
    {
        var p = await _svc.RecordVideoAsync(_user.Id, item, from, duration, 0);
        for (var pos = from + 10; pos <= until; pos += 10)
        {
            _clock.Advance(10);
            p = await _svc.RecordVideoAsync(_user.Id, item, pos, duration, 10);
        }
        return p;
    }

    [Fact]
    public async Task Watching_most_of_a_video_completes_it()
    {
        var item = AddItem(ContentType.Video, 100);
        var p = await Play(item, 0, 80);
        Assert.False(p.IsCompleted);
        p = await Play(item, 80, 90);
        Assert.True(p.IsCompleted);
        Assert.Equal(_clock.Now.UtcDateTime, p.CompletedAt);
        Assert.Equal(90, p.SecondsWatched);
    }

    [Fact]
    public async Task Seeking_to_the_end_does_not_complete_a_video()
    {
        var item = AddItem(ContentType.Video, 600);
        await _svc.RecordVideoAsync(_user.Id, item, 0, null, 0);
        _clock.Advance(10);
        var p = await _svc.RecordVideoAsync(_user.Id, item, 600, null, 10);
        Assert.False(p.IsCompleted);
        Assert.Equal(25, p.MaxPositionSeconds); // 10 s at up to 2x speed, plus 5 s slack
        Assert.Equal(600, p.LastPositionSeconds);
    }

    [Fact]
    public async Task Claimed_play_time_is_capped_by_real_time()
    {
        var item = AddItem(ContentType.Video, 600);
        await _svc.RecordVideoAsync(_user.Id, item, 0, null, 0);
        _clock.Advance(5);
        var p = await _svc.RecordVideoAsync(_user.Id, item, 500, null, 500);
        Assert.Equal(15, p.MaxPositionSeconds);
        Assert.Equal(5, p.SecondsWatched);
    }

    [Fact]
    public async Task Duration_is_learned_from_the_player_when_unknown()
    {
        var item = AddItem(ContentType.Video);
        await Play(item, 0, 40, duration: 40);
        Assert.Equal(40, item.DurationSeconds);
        Assert.True((await _db.ContentProgress.SingleAsync()).IsCompleted);
    }

    [Fact]
    public async Task Course_completes_when_its_last_step_is_done()
    {
        var a = AddItem(ContentType.Article);
        var v = AddItem(ContentType.Video, 30);
        var course = new Course
        {
            Title = "C", IsPublished = true,
            Items = [new CourseItem { ContentItem = a, Position = 1 }, new CourseItem { ContentItem = v, Position = 2 }],
        };
        _db.Courses.Add(course);
        _db.SaveChanges();

        await _svc.EnrollAsync(_user.Id, course.Id);
        await _svc.RecordArticleAsync(_user.Id, a.Id, completed: true);
        Assert.Null((await _db.CourseEnrollments.SingleAsync()).CompletedAt);

        await Play(v, 0, 30);
        var enrollment = await _db.CourseEnrollments.SingleAsync();
        Assert.NotNull(enrollment.CompletedAt);

        var status = (await _svc.CourseStatusesAsync(_user.Id, [course.Id]))[course.Id];
        Assert.Equal(100, status.Percent);
    }

    [Fact]
    public async Task Enrolling_after_finishing_every_step_completes_the_course_at_once()
    {
        var a = AddItem(ContentType.Article);
        var course = new Course { Title = "C", IsPublished = true, Items = [new CourseItem { ContentItem = a, Position = 1 }] };
        _db.Courses.Add(course);
        _db.SaveChanges();

        await _svc.RecordArticleAsync(_user.Id, a.Id, completed: true);
        var e = await _svc.EnrollAsync(_user.Id, course.Id, assignedById: _user.Id, dueDate: null);
        Assert.NotNull(e.CompletedAt);
    }

    [Fact]
    public void Steps_unlock_one_at_a_time_when_order_is_enforced()
    {
        var states = ProgressService.ItemStates([1, 2, 3], new HashSet<int> { 1 }, enforceOrder: true);
        Assert.Equal([false, false, true], states.Select(s => s.Locked));

        var free = ProgressService.ItemStates([1, 2, 3], new HashSet<int>(), enforceOrder: false);
        Assert.All(free, s => Assert.False(s.Locked));
    }
}
