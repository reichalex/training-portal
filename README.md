# Training Portal

A web portal for training videos and articles, organised into modules (Power BI, Genie, …) with
nested sections, ordered courses that people work through step by step, and a record of who has
watched or read what.

- **Frontend:** Angular 21 (`web/`)
- **Backend:** ASP.NET Core 8 Web API (`api/TrainingPortal.Api/`)
- **Database:** SQL Server 2019 or later (Entity Framework Core migrations)

## What it does

**For learners**

- **Home:** courses assigned to them (with due dates), courses in progress, videos to pick up
  where they left off, and their totals.
- **Browse:** modules, then their sections (nested as deep as needed), then the videos and articles
  in each. Every module shows how much of it the learner has completed.
- **Videos** resume where the learner stopped. The learner cannot skip ahead past the furthest
  point they have watched until the video is complete. A video counts as watched at 90% (configurable).
- **Articles** are written in Markdown, with images, tables and code. The learner marks them as read.
- **Courses** are a sequence of videos and articles. When "in order" is on, each step unlocks
  only after the previous one is finished. The course completes when every step is done.
- **Search** across titles and summaries.

**For admins** (the **Admin** menu)

- **Modules:** create top-level modules and sections inside them, reorder them, and publish or hide them.
- **Videos & articles:** upload videos (MP4, WebM or MOV, up to 4 GB by default) with a progress
  bar, or link to a video file. Write articles with live preview and image upload. Save as a
  draft or publish.
- **Courses:** build a course from existing content, reorder its steps, and choose whether order is
  enforced. Assign it to people with a due date, and see each learner's progress.
- **Users:** add people, set admin or learner roles, reset passwords, and deactivate accounts.
- **Reports:** for every person, items started and completed, courses done and overdue, minutes
  watched, and last activity. Drill into one person's full training record, or into one video to
  see who watched it and how far. **Export to CSV** (opens in Excel) for all viewing and all course progress.

## How watch tracking works

While a video plays, the player sends a heartbeat every 10 seconds, and again on pause and at the
end, with the position and how long it actually played. The server stores, for each user and item:

| Field | Meaning |
|---|---|
| `MaxPositionSeconds` | Furthest point reached by playing. Each heartbeat can move it forward by at most the real time elapsed × 2 (the fastest playback speed) + 5 s, so jumping to the end earns nothing. |
| `SecondsWatched` | Total time actually played. |
| `LastPositionSeconds` | Where to resume. |
| `IsCompleted` / `CompletedAt` | Set once `MaxPositionSeconds` reaches 90% of the duration. |
| `FirstViewedAt` / `LastViewedAt` | When the person first and last opened the item. |

The browser also blocks seeking ahead, but the server's rule is what counts. Video files are
streamed with HTTP range requests, so seeking and large files work. The URLs carry a signed
6-hour ticket for that user and that video, so a copied link stops working.

Content that sits in a hidden (unpublished) module but belongs to a published course can be
opened only through that course, which is useful for course-only material.

## Running it locally

Prerequisites: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0),
[Node.js](https://nodejs.org/) 20.19+ or 22.12+, and SQL Server (or Docker).

```bash
# 1. SQL Server (skip if you have one; then edit the connection string below)
docker compose up -d

# 2. API on http://localhost:5110
cd api/TrainingPortal.Api
dotnet run
```

On first start the API creates the database, then an admin account and some sample modules
(from `appsettings.Development.json`):

- Email: `admin@example.com`
- Password: `ChangeMe123!`

```bash
# 3. Angular dev server on http://localhost:4200 (forwards /api to the API)
cd web
npm install
npm start
```

Open http://localhost:4200 and sign in. Swagger is at http://localhost:5110/swagger.

To use your own SQL Server, set `ConnectionStrings:Portal` in
`api/TrainingPortal.Api/appsettings.Development.json`. For example, with Windows authentication:
`Server=MYSERVER;Database=TrainingPortal;Trusted_Connection=True;TrustServerCertificate=True`.

## Configuration

Settings are in `appsettings.json`. Override them per environment with
`appsettings.Production.json`, environment variables (`Jwt__Key`, `ConnectionStrings__Portal`, …),
or Azure App Service settings.

| Setting | Default | Notes |
|---|---|---|
| `ConnectionStrings:Portal` | local SQL Server | **Required.** |
| `Jwt:Key` | (empty) | **Required:** a random secret of at least 32 characters. The app refuses to start without it. Keep it out of source control. |
| `Jwt:LifetimeHours` | 8 | How long a sign-in lasts. |
| `Storage:RootPath` | `storage` | Folder for uploaded videos and images (relative to the app, or absolute, e.g. a network share). |
| `Storage:MaxVideoBytes` | 4 GB | Largest video upload. |
| `Progress:CompletionThreshold` | 0.9 | Share of a video that must be watched. |
| `Progress:MaxPlaybackRate` | 2.0 | Fastest playback speed credited. |
| `Seed:AdminEmail` / `Seed:AdminPassword` | (empty) | Creates the first admin when the Users table is empty. Remove after first start. |
| `Seed:SampleContent` | false | Adds the example Power BI and Genie modules to an empty database. |
| `DataProtection:KeyPath` | `storage/keys` | Keys that sign video links. On more than one server, point this at a shared folder. |

## Database

The schema is managed with EF Core migrations (`api/TrainingPortal.Api/Data/Migrations`) and is
applied automatically when the API starts. If your DBAs prefer to run scripts themselves, use
[`database/TrainingPortal.sql`](database/TrainingPortal.sql). It is idempotent and generated with:

```bash
dotnet tool restore
cd api/TrainingPortal.Api
dotnet ef migrations script --idempotent -o ../../database/TrainingPortal.sql
```

After changing the entities in `Data/Entities.cs`, add a migration with
`dotnet ef migrations add <Name> -o Data/Migrations`.

Tables: `Users`, `Modules` (self-referencing `ParentId` for sections), `ContentItems`, `Courses`,
`CourseItems` (ordered steps), `CourseEnrollments` (assignments, due dates, completion), and
`ContentProgress` (one row per person per item).

## Deploying

The Angular build is written straight into the API's `wwwroot`, so one site serves both:

```bash
cd web && npm ci && npx ng build
cd ../api/TrainingPortal.Api && dotnet publish -c Release -o ../../publish
```

Deploy the `publish` folder to **IIS** (install the
[.NET 8 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/8.0)) or **Azure App
Service**. Set `ConnectionStrings__Portal` and `Jwt__Key`, and make sure the app pool identity can
write to `Storage:RootPath`. The included `web.config` raises IIS's 30 MB upload limit.

For larger libraries, store videos in Azure Blob Storage: implement `IFileStorage`
(`Services/FileStorage.cs`) and register it in `Program.cs` in place of `LocalFileStorage`.

## Single sign-on

The portal ships with its own email and password sign-in so it works anywhere. To sign in with
company accounts (Microsoft Entra ID / Azure AD):

1. Register the app in Entra ID.
2. In `Program.cs`, replace the JWT validation with `AddMicrosoftIdentityWebApi`
   (package `Microsoft.Identity.Web`).
3. On first sign-in, create or match the `Users` row by email (`AuthController`).
4. In Angular, replace `AuthService.login` with MSAL (`@azure/msal-angular`).

Roles, progress tracking and reports are unchanged.

## Tests

```bash
dotnet test
```

These cover the watch-tracking rules (skipping ahead, inflated play time, completion threshold,
learned duration), course sequencing and completion, sign-in and admin-only access, hidden
content, video upload and ticketed range streaming, reports and CSV export, and module-tree cycle
protection. The HTTP tests run the real API in memory against SQLite. Production uses SQL Server.

## Project layout

```
api/TrainingPortal.Api/
  Controllers/          learner endpoints (modules, content, courses, progress, media, me)
  Controllers/Admin/    admin endpoints (modules, content + uploads, courses + assignment, users, reports)
  Data/                 entities, DbContext, migrations, seeding
  Services/             progress rules, file storage, tokens, signed video links
api/TrainingPortal.Api.Tests/
web/src/app/
  pages/                learner pages
  admin/                admin pages (lazy-loaded)
  shared/               video player, cards, pipes
  core/                 API clients, auth, models
database/TrainingPortal.sql
```
