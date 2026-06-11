namespace StopMe.Core.ValueObjects;

public sealed class StatsSummary
{
    public required int SessionsCompleted { get; init; }
    public required int FocusMinutes { get; init; }
    public required int WebsiteBlockCount { get; init; }
    public required int AppBlockCount { get; init; }
    public required int BypassAttemptCount { get; init; }
    public required int ProductiveDayStreak { get; init; }
}

public sealed class DailyBucket
{
    public required DateOnly Date { get; init; }
    public required int FocusMinutes { get; init; }
    public required int WebsiteBlockCount { get; init; }
    public required int AppBlockCount { get; init; }
    public required int SessionsCompleted { get; init; }
}

public sealed class TopDistraction
{
    public required string Label { get; init; }
    public required int BlockCount { get; init; }
    public required string Kind { get; init; }   // "Website" or "App"
}

public sealed class StatsSnapshot
{
    public required StatsWindow Window { get; init; }
    public required StatsSummary Summary { get; init; }
    public required IReadOnlyList<DailyBucket> Daily { get; init; }
    public required IReadOnlyList<TopDistraction> TopDistractions { get; init; }
}
