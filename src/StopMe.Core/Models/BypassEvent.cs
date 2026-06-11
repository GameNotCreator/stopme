using StopMe.Core.Enums;

namespace StopMe.Core.Models;

public class BypassEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public BypassEventType Type { get; set; }

    public Guid? SessionId { get; set; }

    public string? Source { get; set; }

    public string? Detail { get; set; }

    public DateTime DetectedAtUtc { get; set; }
}
