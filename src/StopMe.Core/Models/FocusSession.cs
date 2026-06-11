using StopMe.Core.Enums;

namespace StopMe.Core.Models;

public class FocusSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserProfileId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime StartAtUtc { get; set; }

    public DateTime EndAtUtc { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

    public SessionMode Mode { get; set; } = SessionMode.Soft;

    public LockPolicy LockPolicy { get; set; } = LockPolicy.None;

    public bool IsRecurring { get; set; }

    public string? RecurrenceRule { get; set; }

    public string TimeZoneId { get; set; } = "UTC";

    public bool AllowPauses { get; set; }

    public Guid? PausePolicyId { get; set; }

    public PausePolicy? PausePolicy { get; set; }

    public string? PasswordHash { get; set; }

    public string? RequiredText { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<SessionBlocklist> SessionBlocklists { get; set; } = new List<SessionBlocklist>();
}
