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
  With Microsoft sign-in on, people can sign in with their company account (see below).
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
| `Entra:TenantId` / `Entra:ClientId` | (empty) | Turn on **Sign in with Microsoft**. See [Signing in with Microsoft](#signing-in-with-microsoft-entra-id). |
| `Entra:AutoProvision` | true | Create a learner account for anyone in the tenant on first sign-in. When false, an admin must add people first. |
| `Auth:LocalLogin` | true | Allow email and password sign-in. |
| `Seed:AdminEmail` / `Seed:AdminPassword` | (empty) | Creates the first admin when the Users table is empty. Leave the password empty if the admin signs in with Microsoft. |
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

## Signing in with Microsoft (Entra ID)

People can sign in with their company Microsoft account. The browser signs in with Microsoft
(MSAL), and the API checks the Microsoft token (signature, your tenant, this app, a user rather
than an app) and swaps it for a portal session.

- **First sign-in:** the person is matched to a portal account by email, so accounts an admin
  added beforehand keep their role. Anyone else from your tenant gets a new **learner** account,
  unless `Entra:AutoProvision` is `false`, in which case only people an admin has added can get in.
- **Admins** are still chosen on the **Users** page. A person's role doesn't come from Entra.
- **Deactivating** someone on the Users page stops them signing in, even with a working Microsoft
  account. A session that is already open runs until it expires (`Jwt:LifetimeHours`).
- **Password sign-in** stays available alongside it (useful for a break-glass admin). Turn it off
  with `Auth:LocalLogin: false`.

### One-time setup in the Azure portal

1. **Microsoft Entra ID → App registrations → New registration.** Name it "Training Portal" and
   choose *Accounts in this organizational directory only*.
2. Under **Redirect URI**, choose platform **Single-page application (SPA)** and enter
   `https://<your portal>/login`. For development, also add `http://localhost:4200/login`.
3. **Expose an API:** set the Application ID URI (accept the default `api://<client id>`), then
   **Add a scope** named `access_as_user` (*Admins and users* can consent; it's enabled).
4. Optional: **Manifest:** set `api.requestedAccessTokenVersion` to `2` (older manifests call it
   `accessTokenAcceptedVersion`). Both v1 and v2 tokens are accepted.
5. Optional: under **Enterprise applications → Training Portal → Properties**, set
   *Assignment required* to Yes to choose exactly which people or groups can sign in.
6. Copy the **Directory (tenant) ID** and **Application (client) ID** from the Overview page.

Then configure the API (in `appsettings.Production.json` or as environment variables such as
`Entra__TenantId`):

```json
"Entra": {
  "TenantId": "<directory (tenant) id>",
  "ClientId": "<application (client) id>"
},
"Auth": { "LocalLogin": true },
"Seed": { "AdminEmail": "you@yourcompany.com", "AdminPassword": "" }
```

With `Seed:AdminPassword` left empty on a new database, the first admin has no password. Their
account is linked the first time they sign in with Microsoft using that email. There is no client
secret: the SPA uses PKCE, and the API only checks token signatures against Microsoft's public keys.

## Tests

```bash
dotnet test
```

These cover the watch-tracking rules (skipping ahead, inflated play time, completion threshold,
learned duration), course sequencing and completion, sign-in and admin-only access, Microsoft
token validation (wrong tenant, wrong app, expired, forged, app-only) and account linking, hidden
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
