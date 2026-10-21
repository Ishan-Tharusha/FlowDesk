namespace FlowDesk.Domain;

public static class Roles
{
    public const string TenantAdmin = "TenantAdmin";
    public const string Approver = "Approver";
    public const string Requester = "Requester";

    public static readonly string[] All = [TenantAdmin, Approver, Requester];
}