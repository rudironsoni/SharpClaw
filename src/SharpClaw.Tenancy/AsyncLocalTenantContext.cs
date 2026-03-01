using SharpClaw.Tenancy.Abstractions;

namespace SharpClaw.Tenancy;

/// <summary>
/// AsyncLocal-based tenant context for flowing tenant information across async boundaries.
/// </summary>
public sealed class AsyncLocalTenantContext : ITenantContext
{
    private static readonly AsyncLocal<string> _tenantId = new();
    private static readonly AsyncLocal<string> _tenantName = new();
    
    /// <summary>
    /// Gets the current tenant context.
    /// </summary>
    public static AsyncLocalTenantContext Current { get; } = new AsyncLocalTenantContext();
    
    public string TenantId => _tenantId.Value ?? string.Empty;
    public string TenantName => _tenantName.Value ?? "default";
    public bool IsValid => !string.IsNullOrEmpty(_tenantId.Value);
    
    public static void Set(string tenantId, string tenantName)
    {
        _tenantId.Value = tenantId;
        _tenantName.Value = tenantName;
    }
    
    public static void Clear()
    {
        _tenantId.Value = string.Empty;
        _tenantName.Value = string.Empty;
    }
}
