namespace StopMe.Core.Models;

public class UserProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DisplayName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public bool IsLocalOnly { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
