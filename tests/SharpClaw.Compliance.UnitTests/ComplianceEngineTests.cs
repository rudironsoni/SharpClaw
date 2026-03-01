using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SharpClaw.Compliance.Abstractions;
using SharpClaw.Tenancy.Abstractions;

namespace SharpClaw.Compliance.UnitTests;

public sealed class ComplianceEngineTests
{
    private readonly IComplianceRuleRegistry _ruleRegistry;
    private readonly ITenantComplianceProvider? _tenantProvider;
    private readonly ILogger<ComplianceEngine> _logger;
    private readonly ComplianceEngineOptions _options;

    public ComplianceEngineTests()
    {
        _ruleRegistry = Substitute.For<IComplianceRuleRegistry>();
        _tenantProvider = Substitute.For<ITenantComplianceProvider>();
        _logger = Substitute.For<ILogger<ComplianceEngine>>();
        _options = new ComplianceEngineOptions
        {
            EnableSOC2 = true,
            EnableGDPR = true,
            EnableHIPAA = false,
            EnableISO27001 = true
        };
    }

    private ComplianceEngine CreateEngine()
    {
        var optionsWrapper = Options.Create(_options);
        return new ComplianceEngine(_ruleRegistry, optionsWrapper, _logger, _tenantProvider);
    }

    [Fact]
    public async Task ValidateAsync_WithNullOperation_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = CreateEngine();
        var tenantContext = CreateTenantContext();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            engine.ValidateAsync<object>(null!, tenantContext));
    }

    [Fact]
    public async Task ValidateAsync_WithNullTenantContext_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = CreateEngine();
        var operation = CreateOperation("test");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            engine.ValidateAsync(operation, null!));
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidTenant_ReturnsErrorResult()
    {
        // Arrange
        var engine = CreateEngine();
        var operation = CreateOperation("test");
        var invalidTenant = Substitute.For<ITenantContext>();
        invalidTenant.IsValid.Returns(false);

        // Act
        var result = await engine.ValidateAsync(operation, invalidTenant);

        // Assert
        result.HasErrors.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.RuleId == "TENANT-001");
    }

    [Fact]
    public async Task ValidateAsync_NoApplicableRules_ReturnsSuccess()
    {
        // Arrange
        var engine = CreateEngine();
        var operation = CreateOperation("test");
        var tenantContext = CreateTenantContext();

        _tenantProvider!.GetEnabledStandardsAsync("tenant-1", Arg.Any<CancellationToken>()).Returns(new[] { ComplianceStandard.SOC2 });
        _ruleRegistry.GetRulesForStandard(ComplianceStandard.SOC2).Returns(Array.Empty<IComplianceRule>());

        // Act
        var result = await engine.ValidateAsync(operation, tenantContext);

        // Assert
        result.IsCompliant.Should().BeTrue();
        result.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_WithPassingRules_ReturnsSuccess()
    {
        // Arrange
        var engine = CreateEngine();
        var operation = CreateOperation("test");
        var tenantContext = CreateTenantContext();
        var rule = Substitute.For<IComplianceRule>();
        rule.RuleId.Returns("TEST-001");
        rule.Standard.Returns(ComplianceStandard.SOC2);

        _tenantProvider!.GetEnabledStandardsAsync("tenant-1", Arg.Any<CancellationToken>()).Returns(new[] { ComplianceStandard.SOC2 });
        _ruleRegistry.GetRulesForStandard(ComplianceStandard.SOC2).Returns(new[] { rule });
        rule.EvaluateAsync(Arg.Any<ComplianceContext>(), tenantContext, Arg.Any<CancellationToken>())
            .Returns(ComplianceValidationResult.Compliant("TEST-001"));

        // Act
        var result = await engine.ValidateAsync(operation, tenantContext);

        // Assert
        result.IsCompliant.Should().BeTrue();
        result.Results.Should().ContainSingle(r => r.IsCompliant);
    }

    [Fact]
    public async Task ValidateAsync_WithFailingRule_ReturnsFailure()
    {
        // Arrange
        var engine = CreateEngine();
        var operation = CreateOperation("test");
        var tenantContext = CreateTenantContext();
        var rule = Substitute.For<IComplianceRule>();
        rule.RuleId.Returns("TEST-001");
        rule.Standard.Returns(ComplianceStandard.SOC2);

        _tenantProvider!.GetEnabledStandardsAsync("tenant-1", Arg.Any<CancellationToken>()).Returns(new[] { ComplianceStandard.SOC2 });
        _ruleRegistry.GetRulesForStandard(ComplianceStandard.SOC2).Returns(new[] { rule });
        rule.EvaluateAsync(Arg.Any<ComplianceContext>(), tenantContext, Arg.Any<CancellationToken>())
            .Returns(ComplianceValidationResult.NonCompliant("TEST-001", "Test violation", ComplianceSeverity.Error));

        // Act
        var result = await engine.ValidateAsync(operation, tenantContext);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.HasErrors.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.RuleId == "TEST-001");
    }

    [Fact]
    public async Task ValidateAsync_RuleThrowsException_ReturnsError()
    {
        // Arrange
        var engine = CreateEngine();
        var operation = CreateOperation("test");
        var tenantContext = CreateTenantContext();
        var rule = Substitute.For<IComplianceRule>();
        rule.RuleId.Returns("TEST-001");
        rule.Standard.Returns(ComplianceStandard.SOC2);

        _tenantProvider!.GetEnabledStandardsAsync("tenant-1", Arg.Any<CancellationToken>()).Returns(new[] { ComplianceStandard.SOC2 });
        _ruleRegistry.GetRulesForStandard(ComplianceStandard.SOC2).Returns(new[] { rule });
        rule.EvaluateAsync(Arg.Any<ComplianceContext>(), tenantContext, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<ComplianceValidationResult>(new InvalidOperationException("Test exception")));

        // Act
        var result = await engine.ValidateAsync(operation, tenantContext);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.RuleId == "TEST-001" && e.Message.Contains("Test exception"));
    }

    [Fact]
    public async Task ValidateAsync_SpecificStandard_NotEnabled_ReturnsSuccess()
    {
        // Arrange
        var engine = CreateEngine();
        var operation = CreateOperation("test");
        var tenantContext = CreateTenantContext();

        // HIPAA is disabled in options
        _tenantProvider!.GetEnabledStandardsAsync("tenant-1", Arg.Any<CancellationToken>()).Returns(new[] { ComplianceStandard.SOC2 });

        // Act
        var result = await engine.ValidateAsync(operation, ComplianceStandard.HIPAA, tenantContext);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithWarningOnly_ReturnsCompliantButWithWarnings()
    {
        // Arrange
        var engine = CreateEngine();
        var operation = CreateOperation("test");
        var tenantContext = CreateTenantContext();
        var rule = Substitute.For<IComplianceRule>();
        rule.RuleId.Returns("TEST-001");
        rule.Standard.Returns(ComplianceStandard.SOC2);

        _tenantProvider!.GetEnabledStandardsAsync("tenant-1", Arg.Any<CancellationToken>()).Returns(new[] { ComplianceStandard.SOC2 });
        _ruleRegistry.GetRulesForStandard(ComplianceStandard.SOC2).Returns(new[] { rule });
        rule.EvaluateAsync(Arg.Any<ComplianceContext>(), tenantContext, Arg.Any<CancellationToken>())
            .Returns(ComplianceValidationResult.NonCompliant("TEST-001", "Test warning", ComplianceSeverity.Warning));

        // Act
        var result = await engine.ValidateAsync(operation, tenantContext);

        // Assert
        result.IsCompliant.Should().BeTrue(); // Warnings don't block
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.RuleId == "TEST-001");
    }

    private static ComplianceOperation<T> CreateOperation<T>(T data) => new()
    {
        OperationType = "TestOperation",
        Data = data,
        Classification = DataClassification.Internal
    };

    private static ITenantContext CreateTenantContext()
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.TenantId.Returns("tenant-1");
        tenantContext.IsValid.Returns(true);
        return tenantContext;
    }
}
