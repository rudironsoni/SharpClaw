namespace SharpClaw.Cloud.Azure.Auth;

/// <summary>
/// Service Principal credentials for Azure authentication.
/// </summary>
public sealed class ServicePrincipalCredentials
{
    /// <summary>
    /// Gets or sets the Azure AD application (client) ID.
    /// </summary>
    public required string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the Azure AD application (client) secret.
    /// </summary>
    public required string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the Azure AD tenant ID.
    /// </summary>
    public required string TenantId { get; set; }
}
