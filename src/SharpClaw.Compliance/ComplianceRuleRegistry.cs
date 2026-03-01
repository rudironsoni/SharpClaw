using System.Collections.Frozen;
using SharpClaw.Compliance.Abstractions;

namespace SharpClaw.Compliance;

/// <summary>
/// Thread-safe registry for compliance rules.
/// </summary>
public sealed class ComplianceRuleRegistry : IComplianceRuleRegistry
{
    private readonly Dictionary<string, IComplianceRule> _rulesById = new();
    private readonly Dictionary<ComplianceStandard, List<IComplianceRule>> _rulesByStandard = new();
    private readonly Lock _lock = new();

    /// <inheritdoc />
    public void RegisterRule(IComplianceRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (string.IsNullOrWhiteSpace(rule.RuleId))
        {
            throw new ArgumentException("Rule ID cannot be empty", nameof(rule));
        }

        lock (_lock)
        {
            _rulesById[rule.RuleId] = rule;

            if (!_rulesByStandard.TryGetValue(rule.Standard, out var rules))
            {
                rules = new List<IComplianceRule>();
                _rulesByStandard[rule.Standard] = rules;
            }

            if (!rules.Any(r => r.RuleId == rule.RuleId))
            {
                rules.Add(rule);
            }
        }
    }

    /// <inheritdoc />
    public bool UnregisterRule(string ruleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);

        lock (_lock)
        {
            if (!_rulesById.TryGetValue(ruleId, out var rule))
            {
                return false;
            }

            _rulesById.Remove(ruleId);

            if (_rulesByStandard.TryGetValue(rule.Standard, out var rules))
            {
                rules.RemoveAll(r => r.RuleId == ruleId);
            }

            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IComplianceRule> GetAllRules()
    {
        lock (_lock)
        {
            return _rulesById.Values.ToFrozenSet();
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<IComplianceRule> GetRulesForStandard(ComplianceStandard standard)
    {
        lock (_lock)
        {
            if (_rulesByStandard.TryGetValue(standard, out var rules))
            {
                return rules.ToFrozenSet();
            }

            return Array.Empty<IComplianceRule>();
        }
    }

    /// <inheritdoc />
    public IComplianceRule? GetRuleById(string ruleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);

        lock (_lock)
        {
            _rulesById.TryGetValue(ruleId, out var rule);
            return rule;
        }
    }
}
