using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.Configure<JwtOptions>(config.GetSection("Jwt"));
builder.Services.Configure<StorageOptions>(config.GetSection("Storage"));
builder.Services.Configure<ProgressOptions>(config.GetSection("Progress"));
builder.Services.Configure<SeedOptions>(config.GetSection("Seed"));

builder.Services.AddDbContext<PortalDbContext>(o =>
    o.UseSqlServer(config.GetConnectionString("Portal")
                   ?? throw new InvalidOperationException("ConnectionStrings:Portal is not set.")));

var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters. Set it in user secrets or an environment variable.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = jwt.SigningKey(),
        ClockSkew = TimeSpan.FromMinutes(1),
    });
builder.Services.AddAuthorization();

// Keys that sign video tickets; kept on disk so links survive a restart. On a web farm,
// point this at a shared folder (or use PersistKeysToAzureBlobStorage etc.).
var storageRoot = Path.Combine(builder.Environment.ContentRootPath, config["Storage:RootPath"] ?? "storage");
builder.Services.AddDataProtection()
    .SetApplicationName("TrainingPortal")
    .PersistKeysToFileSystem(new DirectoryInfo(config["DataProtection:KeyPath"] ?? Path.Combine(storageRoot, "keys")));

builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<MediaLinkService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ProgressService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
    if (db.Database.IsSqlServer()) await db.Database.MigrateAsync();
    else await db.Database.EnsureCreatedAsync();
    await DbSeeder.SeedAsync(db, config.GetSection("Seed").Get<SeedOptions>() ?? new SeedOptions(),
        scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>(), app.Logger);
}

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// The built Angular app is copied into wwwroot for deployment, so one site serves both.
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
