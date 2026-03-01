using SharpClaw.Compliance.Abstractions;
using SharpClaw.Tenancy.Abstractions;

namespace SharpClaw.Compliance.UnitTests;

public sealed class ComplianceRuleRegistryTests
{
    [Fact]
    public void RegisterRule_WithValidRule_AddsRuleToRegistry()
    {
        // Arrange
        var registry = new ComplianceRuleRegistry();
        var rule = new TestComplianceRule("TEST-001", ComplianceStandard.SOC2);

        // Act
        registry.RegisterRule(rule);

        // Assert
        var allRules = registry.GetAllRules();
        Assert.Single(allRules);
        Assert.Equal("TEST-001", allRules.First().RuleId);
    }

    [Fact]
    public void RegisterRule_WithNullRule_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = new ComplianceRuleRegistry();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.RegisterRule(null!));
    }

    [Fact]
    public void RegisterRule_WithEmptyRuleId_ThrowsArgumentException()
    {
        // Arrange
        var registry = new ComplianceRuleRegistry();
        var rule = new TestComplianceRule("", ComplianceStandard.SOC2);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.RegisterRule(rule));
    }

    [Fact]
    public void UnregisterRule_ExistingRule_ReturnsTrue()
    {
        // Arrange
        var registry = new ComplianceRuleRegistry();
        var rule = new TestComplianceRule("TEST-001", ComplianceStandard.SOC2);
        registry.RegisterRule(rule);

        // Act
        var result = registry.UnregisterRule("TEST-001");

        // Assert
        Assert.True(result);
        Assert.Empty(registry.GetAllRules());
    }

    [Fact]
    public void UnregisterRule_NonExistingRule_ReturnsFalse()
    {
        // Arrange
        var registry = new ComplianceRuleRegistry();

        // Act
        var result = registry.UnregisterRule("NONEXISTENT");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetRulesForStandard_ReturnsOnlyRulesForThatStandard()
    {
        // Arrange
        var registry = new ComplianceRuleRegistry();
        var soc2Rule = new TestComplianceRule("SOC2-001", ComplianceStandard.SOC2);
        var hipaaRule = new TestComplianceRule("HIPAA-001", ComplianceStandard.HIPAA);
        var soc2Rule2 = new TestComplianceRule("SOC2-002", ComplianceStandard.SOC2);

        registry.RegisterRule(soc2Rule);
        registry.RegisterRule(hipaaRule);
        registry.RegisterRule(soc2Rule2);

        // Act
        var soc2Rules = registry.GetRulesForStandard(ComplianceStandard.SOC2);

        // Assert
        Assert.Equal(2, soc2Rules.Count);
        Assert.All(soc2Rules, r => Assert.Equal(ComplianceStandard.SOC2, r.Standard));
    }

    [Fact]
    public void GetRuleById_ExistingRule_ReturnsRule()
    {
        // Arrange
        var registry = new ComplianceRuleRegistry();
        var rule = new TestComplianceRule("TEST-001", ComplianceStandard.SOC2);
        registry.RegisterRule(rule);

        // Act
        var result = registry.GetRuleById("TEST-001");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TEST-001", result.RuleId);
    }

    [Fact]
    public void GetRuleById_NonExistingRule_ReturnsNull()
    {
        // Arrange
        var registry = new ComplianceRuleRegistry();

        // Act
        var result = registry.GetRuleById("NONEXISTENT");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void RegisterRule_DuplicateRuleId_UpdatesExistingRule()
    {
        // Arrange
        var registry = new ComplianceRuleRegistry();
        var rule1 = new TestComplianceRule("TEST-001", ComplianceStandard.SOC2);
        var rule2 = new TestComplianceRule("TEST-001", ComplianceStandard.HIPAA);

        // Act
        registry.RegisterRule(rule1);
        registry.RegisterRule(rule2);

        // Assert
        var allRules = registry.GetAllRules();
        Assert.Single(allRules);
        Assert.Equal(ComplianceStandard.HIPAA, allRules.First().Standard);
    }

    private sealed class TestComplianceRule : IComplianceRule
    {
        public string RuleId { get; }
        public string Description => "Test rule";
        public ComplianceStandard Standard { get; }
        public ComplianceSeverity Severity => ComplianceSeverity.Error;

        public TestComplianceRule(string ruleId, ComplianceStandard standard)
        {
            RuleId = ruleId;
            Standard = standard;
        }

        public Task<ComplianceValidationResult> EvaluateAsync(
            ComplianceContext context,
            ITenantContext tenantContext,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ComplianceValidationResult.Compliant(RuleId));
        }
    }
}
