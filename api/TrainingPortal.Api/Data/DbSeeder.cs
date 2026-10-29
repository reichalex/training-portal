using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace TrainingPortal.Api.Data;

public class SeedOptions
{
    /// <summary>First admin account, created only when the Users table is empty.</summary>
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
    public string AdminName { get; set; } = "Portal Administrator";
    /// <summary>Adds example modules (Power BI, Genie) and articles to an empty catalog.</summary>
    public bool SampleContent { get; set; }
}

public static class DbSeeder
{
    public static async Task SeedAsync(PortalDbContext db, SeedOptions opt, IPasswordHasher<User> hasher, ILogger logger)
    {
        if (!await db.Users.AnyAsync())
        {
            if (string.IsNullOrWhiteSpace(opt.AdminEmail) || string.IsNullOrWhiteSpace(opt.AdminPassword))
            {
                logger.LogWarning("No users exist. Set Seed:AdminEmail and Seed:AdminPassword to create the first admin.");
            }
            else
            {
                var admin = new User
                {
                    Email = opt.AdminEmail.Trim().ToLowerInvariant(),
                    DisplayName = opt.AdminName,
                    Role = UserRole.Admin,
                };
                admin.PasswordHash = hasher.HashPassword(admin, opt.AdminPassword);
                db.Users.Add(admin);
                await db.SaveChangesAsync();
                logger.LogInformation("Created admin account {Email}.", admin.Email);
            }
        }

        if (opt.SampleContent && !await db.Modules.AnyAsync())
        {
            await AddSampleContentAsync(db);
            logger.LogInformation("Added sample modules and content.");
        }
    }

    private static async Task AddSampleContentAsync(PortalDbContext db)
    {
        TrainingModule Module(string name, string description, int order, TrainingModule? parent = null) =>
            new() { Name = name, Description = description, SortOrder = order, Parent = parent, IsPublished = true };

        ContentItem Article(TrainingModule m, string title, string summary, int minutes, string body, int order) =>
            new()
            {
                Module = m, Title = title, Summary = summary, Type = ContentType.Article, EstimatedMinutes = minutes,
                ArticleBody = body, SortOrder = order, IsPublished = true,
            };

        var powerBi = Module("Power BI", "Build reports and dashboards with Microsoft Power BI.", 1);
        var pbiBasics = Module("Getting started", "Connecting to data and building your first report.", 1, powerBi);
        var pbiDax = Module("DAX", "Measures, calculated columns and time intelligence.", 2, powerBi);
        var genie = Module("Genie", "Ask questions of your data in plain language.", 2);
        var genieBasics = Module("Introduction", "What Genie is and when to use it.", 1, genie);

        var welcome = Article(pbiBasics, "Welcome to Power BI", "What Power BI is and how it is used here.", 5,
            """
            # Welcome to Power BI

            Power BI turns data from many sources into interactive reports.

            In this module you will learn to:

            1. Connect to a data source
            2. Shape the data with Power Query
            3. Build and publish a report

            > Replace this sample article with your own material from **Admin → Content**.
            """, 1);
        var measures = Article(pbiDax, "Measures vs calculated columns", "When to use each, with examples.", 8,
            """
            # Measures vs calculated columns

            | | Calculated column | Measure |
            |---|---|---|
            | Evaluated | At refresh, row by row | At query time |
            | Stored | In the model | Not stored |
            | Use for | Slicing and filtering | Aggregations |

            ```
            Total Sales = SUM(Sales[Amount])
            ```
            """, 1);
        var genieIntro = Article(genieBasics, "What is Genie?", "An overview of Genie spaces.", 4,
            """
            # What is Genie?

            Genie lets you ask questions about your data in plain language and get answers as tables and charts.

            **Tip:** be specific about the time period and the measure you want.
            """, 1);

        db.Modules.AddRange(powerBi, pbiBasics, pbiDax, genie, genieBasics);
        db.ContentItems.AddRange(welcome, measures, genieIntro);
        db.Courses.Add(new Course
        {
            Title = "Power BI foundations",
            Description = "Start here: the essentials every report author needs, in order.",
            Module = powerBi,
            EnforceOrder = true,
            IsPublished = true,
            Items = [new CourseItem { ContentItem = welcome, Position = 1 }, new CourseItem { ContentItem = measures, Position = 2 }],
        });
        await db.SaveChangesAsync();
    }
}
