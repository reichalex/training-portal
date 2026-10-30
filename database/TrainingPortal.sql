IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE TABLE [Modules] (
        [Id] int NOT NULL IDENTITY,
        [ParentId] int NULL,
        [Name] nvarchar(200) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [SortOrder] int NOT NULL,
        [IsPublished] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Modules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Modules_Modules_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Modules] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [Email] nvarchar(256) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [Department] nvarchar(200) NULL,
        [PasswordHash] nvarchar(500) NOT NULL,
        [Role] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastLoginAt] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE TABLE [Courses] (
        [Id] int NOT NULL IDENTITY,
        [Title] nvarchar(300) NOT NULL,
        [Description] nvarchar(4000) NULL,
        [ModuleId] int NULL,
        [EnforceOrder] bit NOT NULL,
        [IsPublished] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Courses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Courses_Modules_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [Modules] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE TABLE [ContentItems] (
        [Id] int NOT NULL IDENTITY,
        [ModuleId] int NOT NULL,
        [Title] nvarchar(300) NOT NULL,
        [Summary] nvarchar(2000) NULL,
        [Type] int NOT NULL,
        [VideoFileKey] nvarchar(200) NULL,
        [VideoFileName] nvarchar(300) NULL,
        [VideoContentType] nvarchar(100) NULL,
        [VideoSizeBytes] bigint NULL,
        [ExternalVideoUrl] nvarchar(2000) NULL,
        [DurationSeconds] int NULL,
        [ArticleBody] nvarchar(max) NULL,
        [EstimatedMinutes] int NULL,
        [SortOrder] int NOT NULL,
        [IsPublished] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] int NULL,
        CONSTRAINT [PK_ContentItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContentItems_Modules_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [Modules] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ContentItems_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [Users] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE TABLE [CourseEnrollments] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [CourseId] int NOT NULL,
        [EnrolledAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [AssignedById] int NULL,
        [DueDate] datetime2 NULL,
        CONSTRAINT [PK_CourseEnrollments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CourseEnrollments_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CourseEnrollments_Users_AssignedById] FOREIGN KEY ([AssignedById]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_CourseEnrollments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE TABLE [ContentProgress] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [ContentItemId] int NOT NULL,
        [LastPositionSeconds] int NOT NULL,
        [MaxPositionSeconds] int NOT NULL,
        [SecondsWatched] int NOT NULL,
        [IsCompleted] bit NOT NULL,
        [FirstViewedAt] datetime2 NOT NULL,
        [LastViewedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        CONSTRAINT [PK_ContentProgress] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContentProgress_ContentItems_ContentItemId] FOREIGN KEY ([ContentItemId]) REFERENCES [ContentItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ContentProgress_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE TABLE [CourseItems] (
        [Id] int NOT NULL IDENTITY,
        [CourseId] int NOT NULL,
        [ContentItemId] int NOT NULL,
        [Position] int NOT NULL,
        CONSTRAINT [PK_CourseItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CourseItems_ContentItems_ContentItemId] FOREIGN KEY ([ContentItemId]) REFERENCES [ContentItems] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CourseItems_Courses_CourseId] FOREIGN KEY ([CourseId]) REFERENCES [Courses] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ContentItems_CreatedById] ON [ContentItems] ([CreatedById]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ContentItems_ModuleId_SortOrder] ON [ContentItems] ([ModuleId], [SortOrder]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ContentProgress_ContentItemId] ON [ContentProgress] ([ContentItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ContentProgress_UserId_ContentItemId] ON [ContentProgress] ([UserId], [ContentItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CourseEnrollments_AssignedById] ON [CourseEnrollments] ([AssignedById]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CourseEnrollments_CourseId] ON [CourseEnrollments] ([CourseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CourseEnrollments_UserId_CourseId] ON [CourseEnrollments] ([UserId], [CourseId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CourseItems_ContentItemId] ON [CourseItems] ([ContentItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CourseItems_CourseId_ContentItemId] ON [CourseItems] ([CourseId], [ContentItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Courses_ModuleId] ON [Courses] ([ModuleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Modules_ParentId] ON [Modules] ([ParentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261029125708_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261029125708_InitialCreate', N'8.0.11');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030030013_AddUserExternalId'
)
BEGIN
    ALTER TABLE [Users] ADD [ExternalId] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030030013_AddUserExternalId'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_ExternalId] ON [Users] ([ExternalId]) WHERE [ExternalId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261030030013_AddUserExternalId'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261030030013_AddUserExternalId', N'8.0.11');
END;
GO

COMMIT;
GO

