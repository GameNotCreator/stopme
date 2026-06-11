using StopMe.Core.Enums;

namespace StopMe.Core.Models;

public class BlockRule
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BlocklistId { get; set; }

    public RuleType Type { get; set; }

    public string Value { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Blocklist? Blocklist { get; set; }
}
