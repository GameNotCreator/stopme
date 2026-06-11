using StopMe.Core.ValueObjects;

namespace StopMe.Core.Abstractions;

public interface IRuleCompiler
{
    Task<IReadOnlyList<CompiledRule>> CompileForSessionsAsync(IReadOnlyList<ActiveSessionInstance> sessions, CancellationToken ct = default);
}
