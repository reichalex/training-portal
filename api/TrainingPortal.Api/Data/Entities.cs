namespace TrainingPortal.Api.Data;

public enum UserRole
{
    Learner = 0,
    Admin = 1,
}

public enum ContentType
{
    Video = 0,
    Article = 1,
}

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Department { get; set; }
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Learner;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// A training area such as "Power BI" or "Genie". Modules nest: a module with a
/// ParentId is a section of its parent, and sections can have sections of their own.
/// </summary>
public class TrainingModule
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public TrainingModule? Parent { get; set; }
    public List<TrainingModule> Children { get; set; } = [];
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ContentItem> ContentItems { get; set; } = [];
}

/// <summary>A video or an article that lives in one module.</summary>
public class ContentItem
{
    public int Id { get; set; }
    public int ModuleId { get; set; }
    public TrainingModule Module { get; set; } = null!;
    public string Title { get; set; } = "";
    public string? Summary { get; set; }
    public ContentType Type { get; set; }

    // Video: either an uploaded file (VideoFileKey) or a direct link to a video file.
    public string? VideoFileKey { get; set; }
    public string? VideoFileName { get; set; }
    public string? VideoContentType { get; set; }
    public long? VideoSizeBytes { get; set; }
    public string? ExternalVideoUrl { get; set; }
    public int? DurationSeconds { get; set; }

    // Article: Markdown.
    public string? ArticleBody { get; set; }
    public int? EstimatedMinutes { get; set; }

    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public User? CreatedBy { get; set; }
}

/// <summary>An ordered sequence of content items a learner works through and completes.</summary>
public class Course
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int? ModuleId { get; set; }
    public TrainingModule? Module { get; set; }
    /// <summary>When true, each item unlocks only after the previous one is completed.</summary>
    public bool EnforceOrder { get; set; } = true;
    public bool IsPublished { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<CourseItem> Items { get; set; } = [];
}

public class CourseItem
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    public int Position { get; set; }
}

/// <summary>One row per user per content item they have opened.</summary>
public class ContentProgress
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;
    public int LastPositionSeconds { get; set; }
    /// <summary>Furthest point reached by actually playing (seeking ahead does not count).</summary>
    public int MaxPositionSeconds { get; set; }
    public int SecondsWatched { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime FirstViewedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastViewedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public class CourseEnrollment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    /// <summary>Set when an admin assigned the course rather than the user starting it.</summary>
    public int? AssignedById { get; set; }
    public User? AssignedBy { get; set; }
    public DateTime? DueDate { get; set; }
}
