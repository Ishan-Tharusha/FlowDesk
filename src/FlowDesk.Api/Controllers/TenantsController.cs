using System.Text.RegularExpressions;
using FlowDesk.Application;
using FlowDesk.Domain;
using FlowDesk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Api.Controllers;

[ApiController]
[Route("api/tenants")]
public class TenantsController(AppDbContext db, ITenantContext tenantContext, IPasswordHasher hasher) : ControllerBase
{
    private static readonly Regex SlugRegex = new("^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$", RegexOptions.Compiled);

    public record SignUpRequest(string Name, string Slug, string AdminEmail, string AdminPassword);

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> SignUp(SignUpRequest req)
    {
        var name = req.Name.Trim();
        var slug = req.Slug.Trim().ToLowerInvariant();
        var email = req.AdminEmail.Trim().ToLowerInvariant();

        if (name.Length is 0 or > 200)
            return Problem(title: "Name eka characters 1 ta 200 ta athara wenna one", statusCode: StatusCodes.Status400BadRequest);
        if (!SlugRegex.IsMatch(slug))
            return Problem(title: "Slug eka characters 3-63, a-z, 0-9 saha '-' witharai", statusCode: StatusCodes.Status400BadRequest);
        if (!email.Contains('@') || email.Length > 256)
            return Problem(title: "Invalid email", statusCode: StatusCodes.Status400BadRequest);
        if (req.AdminPassword.Length is < 8 or > 72)
            return Problem(title: "Password characters 8 ta 72 ta athara wenna one", statusCode: StatusCodes.Status400BadRequest);
        if (await db.Tenants.AnyAsync(t => t.Slug == slug))
            return Problem(title: "Slug already taken", statusCode: StatusCodes.Status409Conflict);

        var tenant = new Tenant { Name = name, Slug = slug };
        tenantContext.SetTenant(tenant.Id);

        var admin = new User
        {
            Email = email,
            PasswordHash = hasher.Hash(req.AdminPassword),
            Role = Roles.TenantAdmin
        };

        db.Tenants.Add(tenant);
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCurrent), null,
            new { tenant.Id, tenant.Name, tenant.Slug, AdminEmail = admin.Email });
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantContext.TenantId);
        return tenant is null ? NotFound() : Ok(tenant);
    }
}