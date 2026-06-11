using StopMe.Core.Enums;

namespace StopMe.Core.ValueObjects;

// A template = a named recipe for one session + its blocklists. ITemplateService.Apply
// materialises the recipe into real Blocklist + FocusSession rows.
public sealed class SessionTemplate
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required TimeSpan Duration { get; init; }
    public required SessionMode Mode { get; init; }
    public required LockPolicy LockPolicy { get; init; }
    public required IReadOnlyList<TemplateBlocklist> Blocklists { get; init; }

    // Optional default recurrence (RFC 5545 RRULE subset) when the user wants to
    // schedule the template rather than fire it once. Null = "create as one-shot".
    public string? DefaultRecurrenceRule { get; init; }

    // Whether the template encourages strict mode by default. Soft templates
    // can still be promoted to strict by the user.
    public bool SuggestsStrict => Mode == SessionMode.Strict;
}

public sealed class TemplateBlocklist
{
    public required string Name { get; init; }
    public required BlocklistMode Mode { get; init; }

    // Categories from CategoryDomains to seed as rules.
    public required IReadOnlyList<string> Categories { get; init; }

    // Direct domain rules added on top of the categories.
    public IReadOnlyList<string> Domains { get; init; } = Array.Empty<string>();

    // Direct app rules (Windows process name) added on top.
    public IReadOnlyList<string> ProcessNames { get; init; } = Array.Empty<string>();
}
