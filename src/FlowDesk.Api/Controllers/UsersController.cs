using FlowDesk.Api.Auth;
using FlowDesk.Application;
using FlowDesk.Domain;
using FlowDesk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = Policies.ManageUsers)]
public class UsersController(AppDbContext db, IPasswordHasher hasher) : ControllerBase
{
    public record CreateUserRequest(string Email, string Password, string Role);

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        if (!email.Contains('@'))
            return Problem(title: "Invalid email", statusCode: StatusCodes.Status400BadRequest);
                if (req.Password.Length is < 8 or > 72)
            return Problem(title: "Password characters 8 ta 72 ta athara wenna one", statusCode: StatusCodes.Status400BadRequest);
        if (!Roles.All.Contains(req.Role))
            return Problem(title: $"Role eka weradi. Use: {string.Join(", ", Roles.All)}",
                statusCode: StatusCodes.Status400BadRequest);
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Problem(title: "Email already exists", statusCode: StatusCodes.Status409Conflict);

        var user = new User { Email = email, PasswordHash = hasher.Hash(req.Password), Role = req.Role };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAll), null, new { user.Id, user.Email, user.Role });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.Users.AsNoTracking()
            .Select(u => new { u.Id, u.Email, u.Role, u.IsActive })
            .ToListAsync());
}