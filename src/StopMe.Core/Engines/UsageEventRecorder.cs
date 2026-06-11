using System.Security.Cryptography;
using System.Text;
using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Core.ValueObjects;

namespace StopMe.Core.Engines;

public sealed class UsageEventRecorder : IUsageEventRecorder
{
    public const string PolicySettingKey = "stats.privacy_mode";

    private readonly IUsageEventRepository _events;
    private readonly ISettingsRepository _settings;

    public UsageEventRecorder(IUsageEventRepository events, ISettingsRepository settings)
    {
        _events = events;
        _settings = settings;
    }

    public async Task<bool> RecordAsync(UsageEventDraft draft, DateTime occurredAtUtc, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var policy = await GetActivePolicyAsync(ct).ConfigureAwait(false);
        if (policy == StatsRetentionPolicy.Disabled) return false;

        var keepDisplay = policy == StatsRetentionPolicy.Readable;

        var entity = new UsageEvent
        {
            Id = Guid.NewGuid(),
            Type = draft.Type,
            SessionId = draft.SessionId,
            RuleId = draft.RuleId,
            DomainHash = Hash(draft.DomainLabel),
            AppNameHash = Hash(draft.AppNameLabel),
            DisplayLabel = keepDisplay ? draft.DisplayLabel : null,
            OccurredAtUtc = occurredAtUtc,
        };

        await _events.AddAsync(entity, ct).ConfigureAwait(false);
        return true;
    }

    public async Task<StatsRetentionPolicy> GetActivePolicyAsync(CancellationToken ct = default)
    {
        var raw = await _settings.GetAsync(PolicySettingKey, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw)) return StatsRetentionPolicy.Readable;
        return Enum.TryParse<StatsRetentionPolicy>(raw, ignoreCase: true, out var parsed)
            ? parsed
            : StatsRetentionPolicy.Readable;
    }

    public Task SetActivePolicyAsync(StatsRetentionPolicy policy, CancellationToken ct = default) =>
        _settings.SetAsync(PolicySettingKey, policy.ToString(), ct);

    public static string? Hash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes);
    }
}
