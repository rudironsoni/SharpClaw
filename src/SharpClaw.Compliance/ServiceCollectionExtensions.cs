using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using SharpClaw.Abstractions.Compliance;
using SharpClaw.Compliance.Abstractions;
using SharpClaw.Compliance.Rules;
using SharpClaw.Compliance.Validation;

namespace SharpClaw.Compliance;

/// <summary>
/// Extension methods for registering compliance services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the compliance engine with all default rules.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddComplianceEngine(
        this IServiceCollection services,
        Action<ComplianceEngineOptions>? configureOptions = null)
    {
        services.AddOptions<ComplianceEngineOptions>()
            .Configure(options => configureOptions?.Invoke(options));

        services.AddSingleton<IComplianceRuleRegistry, ComplianceRuleRegistry>();
        services.AddSingleton<IComplianceEngine, ComplianceEngine>();

        // Register default validators
        services.AddSingleton<DataRetentionValidator>();
        services.AddSingleton<EncryptionValidator>();
        services.AddSingleton<AuditLogValidator>();
        services.AddSingleton<AccessControlValidator>();

        // Register standard compliance rules
        services.AddSingleton<IComplianceRule, Soc2ComplianceRules>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComplianceEngineOptions>>();
            return new Soc2ComplianceRules(
                sp.GetRequiredService<EncryptionValidator>(),
                sp.GetRequiredService<AuditLogValidator>(),
                sp.GetRequiredService<AccessControlValidator>(),
                options);
        });

        services.AddSingleton<IComplianceRule, HipaaComplianceRules>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComplianceEngineOptions>>();
            return new HipaaComplianceRules(
                sp.GetRequiredService<EncryptionValidator>(),
                sp.GetRequiredService<AuditLogValidator>(),
                sp.GetRequiredService<DataRetentionValidator>(),
                options);
        });

        services.AddSingleton<IComplianceRule, GdprComplianceRules>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComplianceEngineOptions>>();
            return new GdprComplianceRules(
                sp.GetRequiredService<DataRetentionValidator>(),
                sp.GetRequiredService<EncryptionValidator>(),
                sp.GetRequiredService<AuditLogValidator>(),
                options);
        });

        services.AddSingleton<IComplianceRule, Iso27001ComplianceRules>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComplianceEngineOptions>>();
            return new Iso27001ComplianceRules(
                sp.GetRequiredService<EncryptionValidator>(),
                sp.GetRequiredService<AccessControlValidator>(),
                sp.GetRequiredService<AuditLogValidator>(),
                options);
        });

        return services;
    }

    /// <summary>
    /// Adds a custom compliance rule to the registry.
    /// </summary>
    /// <typeparam name="TRule">The type of the compliance rule.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddComplianceRule<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRule>(this IServiceCollection services)
        where TRule : class, IComplianceRule
    {
        services.AddSingleton<IComplianceRule, TRule>();
        return services;
    }

    /// <summary>
    /// Adds a custom compliance rule to the registry.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="rule">The rule instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddComplianceRule(
        this IServiceCollection services,
        IComplianceRule rule)
    {
        services.AddSingleton<IComplianceRule>(rule);
        return services;
    }

    /// <summary>
    /// Adds tenant-aware compliance configuration provider.
    /// </summary>
    /// <typeparam name="TProvider">The type of the tenant compliance provider.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTenantComplianceProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(this IServiceCollection services)
        where TProvider : class, ITenantComplianceProvider
    {
        services.AddSingleton<ITenantComplianceProvider, TProvider>();
        return services;
    }
}
