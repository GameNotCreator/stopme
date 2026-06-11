using StopMe.Core.Enums;

namespace StopMe.Core.Models;

public class Blocklist
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserProfileId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public BlocklistMode Mode { get; set; } = BlocklistMode.Blacklist;

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<BlockRule> Rules { get; set; } = new List<BlockRule>();
}
