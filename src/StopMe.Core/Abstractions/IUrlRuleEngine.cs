using StopMe.Core.ValueObjects;

namespace StopMe.Core.Abstractions;

public interface IUrlRuleEngine
{
    BlockDecision Evaluate(string rawUrl, IReadOnlyList<CompiledRule> activeRules);
}
