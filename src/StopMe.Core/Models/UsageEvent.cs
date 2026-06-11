using StopMe.Core.Enums;

namespace StopMe.Core.Models;

public class UsageEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? SessionId { get; set; }

    public Guid? RuleId { get; set; }

    public UsageEventType Type { get; set; }

    public string? DomainHash { get; set; }

    public string? AppNameHash { get; set; }

    public string? DisplayLabel { get; set; }

    public DateTime OccurredAtUtc { get; set; }
}
