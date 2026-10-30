using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Dtos;
using TrainingPortal.Api.Services;

namespace TrainingPortal.Api.Controllers.Admin;

[ApiController]
[Authorize(Roles = nameof(UserRole.Admin))]
[Route("api/admin/users")]
public class AdminUsersController(PortalDbContext db, IPasswordHasher<User> hasher, IOptions<EntraOptions> entra)
    : ControllerBase
{
    [HttpGet]
    public async Task<List<UserDto>> List() =>
        (await db.Users.AsNoTracking().OrderBy(u => u.DisplayName).ToListAsync()).Select(UserDto.From).ToList();

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(UserSaveRequest req)
    {
        // People who sign in with Microsoft need no password; their account is linked by email.
        if (string.IsNullOrEmpty(req.Password) && !entra.Value.Enabled)
            return BadRequest(new { message = "A new user needs a password of at least 8 characters." });
        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Someone with that email already exists." });
        var user = new User();
        Apply(user, req, email);
        if (!string.IsNullOrEmpty(req.Password)) user.PasswordHash = hasher.HashPassword(user, req.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return UserDto.From(user);
    }

    /// <summary>Updates a user; a password in the request resets theirs.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> Update(int id, UserSaveRequest req)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();
        var email = req.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email && u.Id != id))
            return Conflict(new { message = "Someone with that email already exists." });
        if (id == User.UserId() && (req.Role != UserRole.Admin || !req.IsActive))
            return BadRequest(new { message = "You cannot remove your own admin access." });
        Apply(user, req, email);
        if (!string.IsNullOrEmpty(req.Password)) user.PasswordHash = hasher.HashPassword(user, req.Password);
        await db.SaveChangesAsync();
        return UserDto.From(user);
    }

    private static void Apply(User user, UserSaveRequest req, string email)
    {
        user.Email = email;
        user.DisplayName = req.DisplayName.Trim();
        user.Department = string.IsNullOrWhiteSpace(req.Department) ? null : req.Department.Trim();
        user.Role = req.Role;
        user.IsActive = req.IsActive;
    }
}
