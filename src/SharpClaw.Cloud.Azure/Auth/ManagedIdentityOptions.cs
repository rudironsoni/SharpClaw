namespace SharpClaw.Cloud.Azure.Auth;

/// <summary>
/// Managed Identity authentication options for Azure.
/// </summary>
public sealed class ManagedIdentityOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to use system-assigned managed identity.
    /// </summary>
    public bool UseSystemAssignedIdentity { get; set; } = true;

    /// <summary>
    /// Gets or sets the client ID of the user-assigned managed identity.
    /// Only used when <see cref="UseSystemAssignedIdentity"/> is false.
    /// </summary>
    public string? UserAssignedIdentityClientId { get; set; }

    /// <summary>
    /// Gets or sets the resource ID of the user-assigned managed identity.
    /// Only used when <see cref="UseSystemAssignedIdentity"/> is false.
    /// </summary>
    public string? UserAssignedIdentityResourceId { get; set; }
}
