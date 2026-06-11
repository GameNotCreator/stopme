using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;

namespace StopMe.Core.Tests.Fakes;

internal static class TestData
{
    public static CompiledRule Rule(
        RuleType type,
        string value,
        BlocklistMode mode = BlocklistMode.Blacklist,
        Guid? blocklistId = null,
        Guid? sessionId = null,
        string? sessionName = null,
        DateTime? sessionEndUtc = null) =>
        new()
        {
            RuleId = Guid.NewGuid(),
            BlocklistId = blocklistId ?? Guid.NewGuid(),
            Type = type,
            Value = value,
            SourceMode = mode,
            SessionId = sessionId,
            SessionName = sessionName,
            SessionEndUtc = sessionEndUtc,
        };
}
