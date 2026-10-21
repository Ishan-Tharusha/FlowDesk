using FlowDesk.Application;
using FlowDesk.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Api.Middleware;

public class TenantResolutionMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Tenant";

    public async Task InvokeAsync(HttpContext ctx, AppDbContext db, ITenantContext tenantContext)
    {
        // 1) Login wela inna user: tenant eka token eken (header eka ignore karanawa)
        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            if (Guid.TryParse(ctx.User.FindFirst("tenant_id")?.Value, out var tokenTenantId))
                tenantContext.SetTenant(tokenTenantId);

            await next(ctx);
            return;
        }

        // 2) /api/auth/* (login, refresh, logout): X-Tenant header eken
        if (ctx.Request.Path.StartsWithSegments("/api/auth"))
        {
            var slug = ctx.Request.Headers[HeaderName].ToString().Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(slug))
            {
                await Results.Problem(
                    title: "Tenant header missing",
                    detail: $"'{HeaderName}' header eka danna one.",
                    statusCode: StatusCodes.Status400BadRequest).ExecuteAsync(ctx);
                return;
            }

            var tenantId = await db.Tenants
                .AsNoTracking()
                .Where(t => t.Slug == slug)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(ctx.RequestAborted);

            if (tenantId is null)
            {
                await Results.Problem(
                    title: "Tenant not found",
                    detail: $"'{slug}' kiyala tenant kenek nehe.",
                    statusCode: StatusCodes.Status404NotFound).ExecuteAsync(ctx);
                return;
            }

            tenantContext.SetTenant(tenantId.Value);
        }

        // 3) Anonymous wena aya: tenant set karanne nehe. Protected endpoints 401 denawa.
        await next(ctx);
    }
}