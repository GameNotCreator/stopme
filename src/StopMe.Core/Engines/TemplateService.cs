using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Core.ValueObjects;

namespace StopMe.Core.Engines;

public sealed class TemplateService : ITemplateService
{
    private readonly IBlocklistRepository _blocklists;
    private readonly ISessionRepository _sessions;
    private readonly ICategoryRepository _categories;
    private readonly TimeProvider _clock;
    private readonly Dictionary<string, SessionTemplate> _catalog;

    public TemplateService(
        IBlocklistRepository blocklists,
        ISessionRepository sessions,
        ICategoryRepository categories,
        TimeProvider clock,
        IReadOnlyList<SessionTemplate>? catalog = null)
    {
        _blocklists = blocklists;
        _sessions = sessions;
        _categories = categories;
        _clock = clock;
        _catalog = (catalog ?? TemplateCatalog.Defaults)
            .ToDictionary(t => t.Key, t => t, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<SessionTemplate> List() => _catalog.Values.ToArray();

    public SessionTemplate? FindByKey(string key) =>
        _catalog.TryGetValue(key, out var t) ? t : null;

    public async Task<TemplateApplicationResult> ApplyAsync(string templateKey, DateTime startAtUtc, Guid userProfileId, CancellationToken ct = default)
    {
        if (!_catalog.TryGetValue(templateKey, out var template))
            return new TemplateApplicationResult { Success = false, Error = $"Unknown template '{templateKey}'." };

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var blocklistIds = new List<Guid>();

        foreach (var spec in template.Blocklists)
        {
            var rules = new List<BlockRule>();

            foreach (var category in spec.Categories)
            {
                var domains = await _categories.GetDomainsAsync(category, ct).ConfigureAwait(false);
                foreach (var domain in domains)
                {
                    rules.Add(new BlockRule
                    {
                        Id = Guid.NewGuid(),
                        Type = RuleType.Domain,
                        Value = domain,
                        IsEnabled = true,
                        CreatedAtUtc = nowUtc,
                    });
                }
            }

            foreach (var domain in spec.Domains)
            {
                var ruleType = spec.Mode == BlocklistMode.Allowlist ? RuleType.ExceptionDomain : RuleType.Domain;
                rules.Add(new BlockRule
                {
                    Id = Guid.NewGuid(),
                    Type = ruleType,
                    Value = domain,
                    IsEnabled = true,
                    CreatedAtUtc = nowUtc,
                });
            }

            foreach (var process in spec.ProcessNames)
            {
                rules.Add(new BlockRule
                {
                    Id = Guid.NewGuid(),
                    Type = RuleType.WindowsProcessName,
                    Value = process,
                    IsEnabled = true,
                    CreatedAtUtc = nowUtc,
                });
            }

            var blocklist = new Blocklist
            {
                Id = Guid.NewGuid(),
                UserProfileId = userProfileId,
                Name = spec.Name,
                Description = $"Generated from template '{template.Key}'",
                Mode = spec.Mode,
                IsEnabled = true,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                Rules = rules,
            };

            await _blocklists.AddAsync(blocklist, ct).ConfigureAwait(false);
            blocklistIds.Add(blocklist.Id);
        }

        var session = new FocusSession
        {
            Id = Guid.NewGuid(),
            UserProfileId = userProfileId,
            Name = template.DisplayName,
            StartAtUtc = startAtUtc,
            EndAtUtc = startAtUtc + template.Duration,
            Status = SessionStatus.Scheduled,
            Mode = template.Mode,
            LockPolicy = template.LockPolicy,
            IsRecurring = !string.IsNullOrWhiteSpace(template.DefaultRecurrenceRule),
            RecurrenceRule = template.DefaultRecurrenceRule,
            TimeZoneId = TimeZoneInfo.Local.Id,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            SessionBlocklists = blocklistIds
                .Select(id => new SessionBlocklist { BlocklistId = id })
                .ToList(),
        };

        await _sessions.AddAsync(session, ct).ConfigureAwait(false);

        return new TemplateApplicationResult
        {
            Success = true,
            SessionId = session.Id,
            BlocklistIds = blocklistIds,
        };
    }
}
