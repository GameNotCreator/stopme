using StopMe.Core.ValueObjects;

namespace StopMe.Core.Abstractions;

public interface IAppRuleEngine
{
    AppBlockDecision Evaluate(RunningAppInfo app, IReadOnlyList<CompiledRule> activeRules);
}
