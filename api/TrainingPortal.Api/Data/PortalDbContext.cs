using Microsoft.EntityFrameworkCore;

namespace TrainingPortal.Api.Data;

public class PortalDbContext(DbContextOptions<PortalDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<TrainingModule> Modules => Set<TrainingModule>();
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseItem> CourseItems => Set<CourseItem>();
    public DbSet<ContentProgress> ContentProgress => Set<ContentProgress>();
    public DbSet<CourseEnrollment> CourseEnrollments => Set<CourseEnrollment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Department).HasMaxLength(200);
            e.Property(x => x.PasswordHash).HasMaxLength(500);
            e.Property(x => x.ExternalId).HasMaxLength(100);
            e.HasIndex(x => x.ExternalId).IsUnique();
        });

        b.Entity<TrainingModule>(e =>
        {
            e.ToTable("Modules");
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasOne(x => x.Parent).WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ContentItem>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Summary).HasMaxLength(2000);
            e.Property(x => x.VideoFileKey).HasMaxLength(200);
            e.Property(x => x.VideoFileName).HasMaxLength(300);
            e.Property(x => x.VideoContentType).HasMaxLength(100);
            e.Property(x => x.ExternalVideoUrl).HasMaxLength(2000);
            e.HasOne(x => x.Module).WithMany(x => x.ContentItems)
                .HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedBy).WithMany()
                .HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => new { x.ModuleId, x.SortOrder });
        });

        b.Entity<Course>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.HasOne(x => x.Module).WithMany()
                .HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CourseItem>(e =>
        {
            e.HasOne(x => x.Course).WithMany(x => x.Items)
                .HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ContentItem).WithMany()
                .HasForeignKey(x => x.ContentItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.CourseId, x.ContentItemId }).IsUnique();
        });

        b.Entity<ContentProgress>(e =>
        {
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ContentItem).WithMany()
                .HasForeignKey(x => x.ContentItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.ContentItemId }).IsUnique();
            e.HasIndex(x => x.ContentItemId);
        });

        b.Entity<CourseEnrollment>(e =>
        {
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Course).WithMany()
                .HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.AssignedBy).WithMany()
                .HasForeignKey(x => x.AssignedById).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => new { x.UserId, x.CourseId }).IsUnique();
        });
    }
}
