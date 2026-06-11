using StopMe.Core.Enums;

namespace StopMe.Core.Models;

public class PausePolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public PauseType Type { get; set; } = PauseType.None;

    public int? FocusMinutes { get; set; }

    public int? BreakMinutes { get; set; }

    public int? DailyAllowanceMinutes { get; set; }

    public bool CountOnlyWhenActiveWindow { get; set; }

    public bool RequireMouseActivity { get; set; }

    public int? MaxPausesPerSession { get; set; }

    public int? PauseDurationMinutes { get; set; }
}
