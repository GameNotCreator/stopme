using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Core.ValueObjects;
using StopMe.LocalApi.Dtos;

namespace StopMe.Service.Ipc;

internal static class Mapping
{
    public static ActiveSessionDto ToDto(ActiveSessionInstance s, DateTime nowUtc) =>
        new()
        {
            SessionId = s.SessionId,
            Name = s.Name,
            StartAtUtc = s.StartAtUtc,
            EndAtUtc = s.EndAtUtc,
            Mode = s.Mode.ToString(),
            LockPolicy = s.LockPolicy.ToString(),
            RemainingSeconds = s.RemainingSeconds(nowUtc),
            BlocklistIds = s.BlocklistIds,
            PausePhase = s.PausePhase?.ToString(),
            PausePhaseRemainingSeconds = s.PausePhaseRemainingSeconds,
        };

    public static BlocklistDto ToDto(Blocklist b) =>
        new()
        {
            Id = b.Id,
            Name = b.Name,
            Description = b.Description,
            Mode = b.Mode.ToString(),
            IsEnabled = b.IsEnabled,
            Rules = b.Rules.Select(ToDto).ToArray(),
        };

    public static BlockRuleDto ToDto(BlockRule r) =>
        new()
        {
            Id = r.Id,
            Type = r.Type.ToString(),
            Value = r.Value,
            IsEnabled = r.IsEnabled,
            Notes = r.Notes,
        };

    public static EvaluateUrlResponse ToResponse(BlockDecision d) =>
        new()
        {
            Blocked = d.IsBlocked,
            Reason = d.Reason,
            MatchedRuleId = d.MatchedRuleId,
            MatchedRuleType = d.MatchedRuleType?.ToString(),
            SessionId = d.SessionId,
            SessionName = d.SessionName,
            RemainingSeconds = d.RemainingSeconds,
        };

    public static EvaluateAppResponse ToResponse(AppBlockDecision d) =>
        new()
        {
            Blocked = d.IsBlocked,
            Reason = d.Reason,
            MatchedRuleId = d.MatchedRuleId,
            MatchedRuleType = d.MatchedRuleType?.ToString(),
            SessionId = d.SessionId,
            SessionName = d.SessionName,
        };

    public static bool TryParseRuleType(string raw, out RuleType type) =>
        Enum.TryParse(raw, ignoreCase: true, out type);

    public static bool TryParseBlocklistMode(string raw, out BlocklistMode mode) =>
        Enum.TryParse(raw, ignoreCase: true, out mode);

    public static bool TryParseStopReason(string raw, out StopReason reason) =>
        Enum.TryParse(raw, ignoreCase: true, out reason);
}
