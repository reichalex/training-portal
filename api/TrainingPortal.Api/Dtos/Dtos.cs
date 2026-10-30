using System.ComponentModel.DataAnnotations;
using TrainingPortal.Api.Data;

namespace TrainingPortal.Api.Dtos;

// ---- Auth / users ----

public record UserDto(int Id, string Email, string DisplayName, string? Department, UserRole Role, bool IsActive,
    DateTime CreatedAt, DateTime? LastLoginAt, bool HasPassword, bool SignsInWithMicrosoft)
{
    public static UserDto From(User u) =>
        new(u.Id, u.Email, u.DisplayName, u.Department, u.Role, u.IsActive, u.CreatedAt, u.LastLoginAt,
            !string.IsNullOrEmpty(u.PasswordHash), u.ExternalId is not null);
}

public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);
public record EntraLoginRequest([Required] string AccessToken);

/// <summary>Which sign-in methods the login page should offer.</summary>
public record AuthConfig(bool LocalLogin, EntraClientConfig? Entra);
public record EntraClientConfig(string ClientId, string Authority, string Scope);
public record LoginResponse(string Token, DateTime ExpiresAt, UserDto User);
public record ChangePasswordRequest([Required] string CurrentPassword, [Required, MinLength(8)] string NewPassword);

// ---- Learner: browsing ----

public record Crumb(int Id, string Name);

public record ModuleTreeNode(int Id, string Name, string? Description, int ContentCount, int CompletedCount,
    List<ModuleTreeNode> Children);

public record ContentSummary(int Id, int ModuleId, string Title, string? Summary, ContentType Type,
    int? DurationSeconds, int? EstimatedMinutes, bool Completed, int Percent, DateTime? LastViewedAt);

public record ModuleDetail(int Id, string Name, string? Description, List<Crumb> Breadcrumb,
    List<ModuleTreeNode> Children, List<ContentSummary> Content, List<CourseSummary> Courses);

public record ProgressDto(int LastPositionSeconds, int MaxPositionSeconds, int SecondsWatched, bool IsCompleted,
    DateTime FirstViewedAt, DateTime LastViewedAt, DateTime? CompletedAt)
{
    public static ProgressDto From(ContentProgress p) => new(p.LastPositionSeconds, p.MaxPositionSeconds,
        p.SecondsWatched, p.IsCompleted, p.FirstViewedAt, p.LastViewedAt, p.CompletedAt);
}

public record CourseContext(int CourseId, string CourseTitle, int Position, int Total,
    int? PreviousContentId, int? NextContentId, bool EnforceOrder, bool CourseCompleted);

public record ContentDetail(int Id, int ModuleId, List<Crumb> Breadcrumb, string Title, string? Summary,
    ContentType Type, string? VideoUrl, int? DurationSeconds, string? ArticleBody, int? EstimatedMinutes,
    DateTime UpdatedAt, ProgressDto? Progress, CourseContext? Course);

public record HeartbeatRequest(int PositionSeconds, int? DurationSeconds, double PlayedSeconds);

public record CourseSummary(int Id, string Title, string? Description, int? ModuleId, string? ModuleName,
    int ItemCount, int CompletedItems, int Percent, DateTime? EnrolledAt, DateTime? CompletedAt,
    DateTime? DueDate, bool Assigned);

public record CourseItemDto(int Position, int ContentId, string Title, ContentType Type, int? DurationSeconds,
    int? EstimatedMinutes, bool Completed, bool Locked);

public record CourseDetail(CourseSummary Course, bool EnforceOrder, List<CourseItemDto> Items);

public record DashboardStats(int CompletedItems, int CompletedCourses, int MinutesWatched);

public record Dashboard(List<CourseSummary> AssignedCourses, List<CourseSummary> InProgressCourses,
    List<ContentSummary> ContinueWatching, List<ContentSummary> RecentlyCompleted, DashboardStats Stats);

// ---- Admin ----

public record AdminModuleDto(int Id, int? ParentId, string Name, string? Description, int SortOrder,
    bool IsPublished, int ContentCount, int ChildCount, string Path);

public record ModuleSaveRequest(int? ParentId, [Required, MaxLength(200)] string Name,
    [MaxLength(2000)] string? Description, int SortOrder, bool IsPublished);

public record AdminContentDto(int Id, int ModuleId, string ModulePath, string Title, string? Summary,
    ContentType Type, string? VideoFileName, long? VideoSizeBytes, string? ExternalVideoUrl, int? DurationSeconds,
    string? ArticleBody, int? EstimatedMinutes, int SortOrder, bool IsPublished, DateTime CreatedAt,
    DateTime UpdatedAt, string? CreatedByName, int ViewerCount, int CompletedCount);

public record ContentSaveRequest(int ModuleId, [Required, MaxLength(300)] string Title,
    [MaxLength(2000)] string? Summary, ContentType Type, [MaxLength(2000), Url] string? ExternalVideoUrl,
    int? DurationSeconds, string? ArticleBody, int? EstimatedMinutes, int SortOrder, bool IsPublished);

public record AdminCourseItem(int ContentId, string Title, ContentType Type, bool IsPublished, string ModulePath);

public record AdminCourseDto(int Id, string Title, string? Description, int? ModuleId, bool EnforceOrder,
    bool IsPublished, DateTime CreatedAt, List<AdminCourseItem> Items, int EnrolledCount, int CompletedCount);

public record CourseSaveRequest([Required, MaxLength(300)] string Title, [MaxLength(4000)] string? Description,
    int? ModuleId, bool EnforceOrder, bool IsPublished, List<int> ContentIds);

public record AssignRequest([Required, MinLength(1)] List<int> UserIds, DateTime? DueDate);

public record UserSaveRequest([Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MaxLength(200)] string DisplayName, [MaxLength(200)] string? Department, UserRole Role,
    bool IsActive, [MinLength(8)] string? Password);

public record UploadResult(string Url);

// ---- Reports ----

public record OverviewReport(int Learners, int Modules, int Videos, int Articles, int Courses,
    int CompletionsLast30Days, int ActiveLearnersLast30Days, List<TopContentRow> TopContent);

public record TopContentRow(int ContentId, string Title, ContentType Type, int Viewers, int Completions);

public record UserReportRow(int UserId, string DisplayName, string Email, string? Department, int ItemsStarted,
    int ItemsCompleted, int CoursesEnrolled, int CoursesCompleted, int CoursesOverdue, int MinutesWatched,
    DateTime? LastActivityAt);

public record UserContentRow(int ContentId, string Title, string ModulePath, ContentType Type, bool IsCompleted,
    int Percent, int SecondsWatched, DateTime FirstViewedAt, DateTime LastViewedAt, DateTime? CompletedAt);

public record UserReportDetail(UserDto User, List<UserContentRow> Content, List<CourseSummary> Courses);

public record ContentViewerRow(int UserId, string DisplayName, string Email, string? Department, int Percent,
    bool IsCompleted, int SecondsWatched, DateTime FirstViewedAt, DateTime LastViewedAt, DateTime? CompletedAt);

public record ContentReport(int ContentId, string Title, ContentType Type, List<ContentViewerRow> Rows);

public record CourseLearnerRow(int UserId, string DisplayName, string Email, string? Department,
    int CompletedItems, int TotalItems, int Percent, DateTime EnrolledAt, DateTime? CompletedAt,
    DateTime? DueDate, bool Assigned, bool Overdue);

public record CourseReport(int CourseId, string Title, List<CourseLearnerRow> Rows);
