using FlowDesk.Application;

namespace FlowDesk.Infrastructure.Tenancy;

// Scoped: request ekakata ekak (request eka iwara unama nathi wenawa)
public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public void SetTenant(Guid tenantId) => TenantId = tenantId;
}