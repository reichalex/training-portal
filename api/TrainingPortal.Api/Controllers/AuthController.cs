using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrainingPortal.Api.Data;
using TrainingPortal.Api.Dtos;
using TrainingPortal.Api.Services;

namespace TrainingPortal.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    PortalDbContext db,
    TokenService tokens,
    IPasswordHasher<User> hasher,
    IOptions<AuthOptions> authOptions,
    IOptions<EntraOptions> entraOptions,
    IEntraTokenValidator entraValidator) : ControllerBase
{
    private readonly AuthOptions _auth = authOptions.Value;
    private readonly EntraOptions _entra = entraOptions.Value;

    [HttpGet("config")]
    [AllowAnonymous]
    public AuthConfig Config() => new(_auth.LocalLogin,
        _entra.Enabled ? new EntraClientConfig(_entra.ClientId!, _entra.Authority, _entra.ResolvedScope) : null);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        if (!_auth.LocalLogin)
            return BadRequest(new { message = "Sign in with your Microsoft account." });
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !user.IsActive || string.IsNullOrEmpty(user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid email or password." });
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, req.Password);

        return await SignIn(user);
    }

    /// <summary>
    /// Exchanges a Microsoft Entra ID access token (obtained by the browser) for a portal
    /// token. The person is matched by their Entra object id, then by email; people not yet
    /// in the portal get a learner account unless Entra:AutoProvision is off.
    /// </summary>
    [HttpPost("entra")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Entra(EntraLoginRequest req)
    {
        if (!_entra.Enabled) return NotFound();
        var identity = await entraValidator.ValidateAsync(req.AccessToken);
        if (identity is null)
            return Unauthorized(new { message = "Microsoft sign-in could not be verified. Please try again." });

        var user = await db.Users.FirstOrDefaultAsync(u => u.ExternalId == identity.ObjectId);
        var sameEmail = await db.Users.FirstOrDefaultAsync(u => u.Email == identity.Email);
        if (user is not null)
        {
            // Follow email changes in the directory, unless another portal account has that email.
            if (sameEmail is null) user.Email = identity.Email;
        }
        else if (sameEmail is not null)
        {
            // An account an admin created (or a password account) for the same email: link it.
            if (sameEmail.ExternalId is not null)
                return Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "This email is already linked to a different Microsoft account. Ask an admin for help.");
            user = sameEmail;
            user.ExternalId = identity.ObjectId;
        }
        else if (_entra.AutoProvision)
        {
            user = new User { Email = identity.Email, DisplayName = identity.DisplayName, ExternalId = identity.ObjectId };
            db.Users.Add(user);
        }
        else
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden,
                detail: "You have not been given access to the training portal. Ask an admin to add you.");
        }

        if (!user.IsActive)
            return Problem(statusCode: StatusCodes.Status403Forbidden, detail: "Your portal account has been deactivated.");
        return await SignIn(user);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await db.Users.FindAsync(User.UserId());
        return user is null || !user.IsActive ? Unauthorized() : UserDto.From(user);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req)
    {
        var user = await db.Users.FindAsync(User.UserId());
        if (user is null) return Unauthorized();
        if (!_auth.LocalLogin || string.IsNullOrEmpty(user.PasswordHash))
            return BadRequest(new { message = "Your account signs in with Microsoft and has no portal password." });
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, req.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest(new { message = "Current password is incorrect." });
        user.PasswordHash = hasher.HashPassword(user, req.NewPassword);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<LoginResponse> SignIn(User user)
    {
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var (token, expires) = tokens.Issue(user);
        return new LoginResponse(token, expires, UserDto.From(user));
    }
}
