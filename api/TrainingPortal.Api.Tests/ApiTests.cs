using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Dtos;

namespace TrainingPortal.Api.Tests;

/// <summary>Runs the real API in memory against SQLite instead of SQL Server.</summary>
public sealed class PortalFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    public string StoragePath { get; } = Path.Combine(Path.GetTempPath(), "portal-tests-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _conn.Open();
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Portal", "unused");
        builder.UseSetting("Jwt:Key", "test-key-test-key-test-key-test-key-1234");
        builder.UseSetting("Storage:RootPath", StoragePath);
        builder.UseSetting("Seed:AdminEmail", "admin@example.com");
        builder.UseSetting("Seed:AdminPassword", "AdminPass1!");
        builder.UseSetting("Seed:SampleContent", "true");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<PortalDbContext>>();
            services.AddDbContext<PortalDbContext>(o => o.UseSqlite(_conn));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _conn.Dispose();
        if (Directory.Exists(StoragePath)) Directory.Delete(StoragePath, recursive: true);
    }
}

file static class ServiceCollectionExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services)
    {
        foreach (var d in services.Where(d => d.ServiceType == typeof(T)).ToList()) services.Remove(d);
    }
}

public sealed class ApiTests : IClassFixture<PortalFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly PortalFactory _factory;

    public ApiTests(PortalFactory factory) => _factory = factory;

    private async Task<HttpClient> Login(string email, string password)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var login = await res.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }

    private Task<HttpClient> Admin() => Login("admin@example.com", "AdminPass1!");

    private async Task<HttpClient> NewLearner()
    {
        var email = $"learner-{Guid.NewGuid():N}@example.com";
        var admin = await Admin();
        (await admin.PostAsJsonAsync("/api/admin/users",
            new { email, displayName = "Learner", role = "Learner", isActive = true, password = "Password1" }))
            .EnsureSuccessStatusCode();
        return await Login(email, "Password1");
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var res = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "admin@example.com", password = "nope" });
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Api_requires_login_and_admin_area_requires_admin()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _factory.CreateClient().GetAsync("/api/modules/tree")).StatusCode);
        var learner = await NewLearner();
        Assert.Equal(HttpStatusCode.OK, (await learner.GetAsync("/api/modules/tree")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await learner.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Learners_see_published_modules_with_nested_sections()
    {
        var learner = await NewLearner();
        var tree = await learner.GetFromJsonAsync<List<ModuleTreeNode>>("/api/modules/tree", Json);
        var powerBi = Assert.Single(tree!, m => m.Name == "Power BI");
        Assert.Contains(powerBi.Children, c => c.Name == "DAX");
        Assert.Equal(2, powerBi.ContentCount);
    }

    [Fact]
    public async Task Unpublished_content_is_hidden_from_learners()
    {
        var admin = await Admin();
        var created = await (await admin.PostAsJsonAsync("/api/admin/content",
                new { moduleId = 1, title = "Draft", type = "Article", articleBody = "wip", isPublished = false }))
            .Content.ReadFromJsonAsync<AdminContentDto>(Json);
        var learner = await NewLearner();
        Assert.Equal(HttpStatusCode.NotFound, (await learner.GetAsync($"/api/content/{created!.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/content/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Course_steps_unlock_in_order_and_the_course_completes()
    {
        var learner = await NewLearner();
        var course = await learner.GetFromJsonAsync<CourseDetail>("/api/courses/1", Json);
        var (first, second) = (course!.Items[0].ContentId, course.Items[1].ContentId);
        Assert.True(course.Items[1].Locked);

        Assert.Equal(HttpStatusCode.Forbidden, (await learner.GetAsync($"/api/content/{second}?courseId=1")).StatusCode);
        (await learner.GetAsync($"/api/content/{first}?courseId=1")).EnsureSuccessStatusCode();
        (await learner.PostAsync($"/api/progress/{first}/article?completed=true", null)).EnsureSuccessStatusCode();
        (await learner.GetAsync($"/api/content/{second}?courseId=1")).EnsureSuccessStatusCode();
        (await learner.PostAsync($"/api/progress/{second}/article?completed=true", null)).EnsureSuccessStatusCode();

        course = await learner.GetFromJsonAsync<CourseDetail>("/api/courses/1", Json);
        Assert.Equal(100, course!.Course.Percent);
        Assert.NotNull(course.Course.CompletedAt);
    }

    [Fact]
    public async Task Admin_can_upload_a_video_and_learners_stream_it_with_a_ticket()
    {
        var admin = await Admin();
        var video = await (await admin.PostAsJsonAsync("/api/admin/content",
                new { moduleId = 2, title = "Clip", type = "Video", isPublished = true }))
            .Content.ReadFromJsonAsync<AdminContentDto>(Json);

        var bytes = Enumerable.Range(0, 5000).Select(i => (byte)i).ToArray();
        using var form = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", "clip.mp4" } };
        (await admin.PostAsync($"/api/admin/content/{video!.Id}/video", form)).EnsureSuccessStatusCode();

        var learner = await NewLearner();
        var detail = await learner.GetFromJsonAsync<ContentDetail>($"/api/content/{video.Id}", Json);
        Assert.StartsWith($"/api/media/video/{video.Id}?t=", detail!.VideoUrl);

        var anonymous = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, detail.VideoUrl);
        request.Headers.Range = new RangeHeaderValue(100, 199);
        var res = await anonymous.SendAsync(request);
        Assert.Equal(HttpStatusCode.PartialContent, res.StatusCode);
        Assert.Equal(bytes[100..200], await res.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/media/video/{video.Id}")).StatusCode);
        // A ticket for one video does not open another.
        var otherUrl = detail.VideoUrl!.Replace($"/video/{video.Id}?", $"/video/{video.Id + 1000}?");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(otherUrl)).StatusCode);
    }

    [Fact]
    public async Task Admin_reports_show_who_has_completed_what()
    {
        var learner = await NewLearner();
        (await learner.PostAsync("/api/progress/3/article?completed=true", null)).EnsureSuccessStatusCode();
        var admin = await Admin();
        var report = await admin.GetFromJsonAsync<ContentReport>("/api/admin/reports/content/3", Json);
        Assert.Contains(report!.Rows, r => r.IsCompleted && r.Percent == 100);
        var csv = await admin.GetStringAsync("/api/admin/reports/export/content.csv");
        Assert.Contains("What is Genie?", csv);
    }

    [Fact]
    public async Task Modules_cannot_be_moved_inside_themselves()
    {
        var admin = await Admin();
        var modules = await admin.GetFromJsonAsync<List<AdminModuleDto>>("/api/admin/modules", Json);
        var powerBi = modules!.Single(m => m.Name == "Power BI");
        var dax = modules!.Single(m => m.Name == "DAX");
        Assert.Equal(powerBi.Id, dax.ParentId);
        var res = await admin.PutAsJsonAsync($"/api/admin/modules/{powerBi.Id}",
            new { parentId = dax.Id, name = "Power BI", sortOrder = 1, isPublished = true });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Assigned_courses_appear_on_the_learner_dashboard()
    {
        var learner = await NewLearner();
        var me = await learner.GetFromJsonAsync<UserDto>("/api/auth/me", Json);
        var admin = await Admin();
        (await admin.PostAsJsonAsync("/api/admin/courses/1/assign",
            new { userIds = new[] { me!.Id }, dueDate = DateTime.UtcNow.AddDays(7) })).EnsureSuccessStatusCode();
        var dash = await learner.GetFromJsonAsync<Dashboard>("/api/me/dashboard", Json);
        var assigned = Assert.Single(dash!.AssignedCourses);
        Assert.NotNull(assigned.DueDate);
    }
}
