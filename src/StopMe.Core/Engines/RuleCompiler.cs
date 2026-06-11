using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;

namespace StopMe.Core.Engines;

public sealed class RuleCompiler : IRuleCompiler
{
    private readonly IBlocklistRepository _blocklists;
    private readonly ICategoryRepository _categories;

    public RuleCompiler(IBlocklistRepository blocklists, ICategoryRepository categories)
    {
        _blocklists = blocklists;
        _categories = categories;
    }

    public async Task<IReadOnlyList<CompiledRule>> CompileForSessionsAsync(IReadOnlyList<ActiveSessionInstance> sessions, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        if (sessions.Count == 0) return Array.Empty<CompiledRule>();

        var blocklistIds = sessions.SelectMany(s => s.BlocklistIds).Distinct().ToArray();
        var blocklists = await _blocklists.GetByIdsAsync(blocklistIds, ct).ConfigureAwait(false);
        var byId = blocklists.ToDictionary(b => b.Id);

        var categoryCache = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CompiledRule>();

        foreach (var session in sessions)
        {
            // Pomodoro Break phase: this session's rules don't enforce. We still keep
            // the session in the published snapshot via SessionEngine (so the UI can
            // render "On break — 4 min left") — but RuleCompiler skips the rule rows.
            if (session.IsOnBreak) continue;

            foreach (var blocklistId in session.BlocklistIds)
            {
                if (!byId.TryGetValue(blocklistId, out var blocklist) || !blocklist.IsEnabled)
                    continue;

                // FullInternet-mode blocklists implicitly emit a synthetic FullInternet rule
                // so the engine treats the blocklist as "block all unless exception".
                if (blocklist.Mode == BlocklistMode.FullInternet)
                {
                    result.Add(new CompiledRule
                    {
                        RuleId = blocklist.Id,
                        BlocklistId = blocklist.Id,
                        Type = RuleType.FullInternet,
                        Value = "*",
                        SourceMode = BlocklistMode.FullInternet,
                        SessionId = session.SessionId,
                        SessionName = session.Name,
                        SessionEndUtc = session.EndAtUtc,
                    });
                }

                foreach (var rule in blocklist.Rules.Where(r => r.IsEnabled))
                {
                    if (rule.Type == RuleType.Category)
                    {
                        if (!categoryCache.TryGetValue(rule.Value, out var domains))
                        {
                            domains = await _categories.GetDomainsAsync(rule.Value, ct).ConfigureAwait(false);
                            categoryCache[rule.Value] = domains;
                        }

                        foreach (var d in domains)
                        {
                            result.Add(new CompiledRule
                            {
                                RuleId = rule.Id,
                                BlocklistId = blocklist.Id,
                                Type = RuleType.Domain,
                                Value = d,
                                SourceMode = blocklist.Mode,
                                SessionId = session.SessionId,
                                SessionName = session.Name,
                                SessionEndUtc = session.EndAtUtc,
                            });
                        }
                    }
                    else
                    {
                        result.Add(new CompiledRule
                        {
                            RuleId = rule.Id,
                            BlocklistId = blocklist.Id,
                            Type = rule.Type,
                            Value = rule.Value,
                            SourceMode = blocklist.Mode,
                            SessionId = session.SessionId,
                            SessionName = session.Name,
                            SessionEndUtc = session.EndAtUtc,
                        });
                    }
                }
            }
        }

        return result;
    }
}
