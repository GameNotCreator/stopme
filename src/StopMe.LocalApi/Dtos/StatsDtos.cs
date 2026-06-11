namespace StopMe.LocalApi.Dtos;

public sealed class StatsQueryRequest
{
    // "Today" | "Last7Days" | "Last30Days" | "AllTime"
    public required string Period { get; init; } = "Last7Days";

    public int TopDistractionsLimit { get; init; } = 5;
}

public sealed class StatsSummaryResponse
{
    public required string Period { get; init; }
    public required DateTime FromUtc { get; init; }
    public required DateTime ToUtc { get; init; }
    public required StatsSummaryDto Summary { get; init; }
    public required IReadOnlyList<StatsDailyBucketDto> Daily { get; init; }
    public required IReadOnlyList<StatsTopDistractionDto> TopDistractions { get; init; }
    public required string PrivacyMode { get; init; }
}

public sealed class StatsSummaryDto
{
    public required int SessionsCompleted { get; init; }
    public required int FocusMinutes { get; init; }
    public required int WebsiteBlockCount { get; init; }
    public required int AppBlockCount { get; init; }
    public required int BypassAttemptCount { get; init; }
    public required int ProductiveDayStreak { get; init; }
}

public sealed class StatsDailyBucketDto
{
    // ISO date "yyyy-MM-dd"
    public required string Date { get; init; }
    public required int FocusMinutes { get; init; }
    public required int WebsiteBlockCount { get; init; }
    public required int AppBlockCount { get; init; }
    public required int SessionsCompleted { get; init; }
}

public sealed class StatsTopDistractionDto
{
    public required string Label { get; init; }
    public required int BlockCount { get; init; }
    public required string Kind { get; init; }
}

public sealed class StatsExportRequest
{
    public required string Period { get; init; } = "Last30Days";
}

public sealed class StatsExportResponse
{
    public required string Filename { get; init; }
    public required string CsvText { get; init; }
}

public sealed class StatsResetResponse
{
    public required int DeletedEvents { get; init; }
}

public sealed class SettingsGetRequest
{
    // null/empty → return everything
    public string? Key { get; init; }
}

public sealed class SettingsGetResponse
{
    public required IReadOnlyDictionary<string, string> Entries { get; init; }
}

public sealed class SettingsSetRequest
{
    public required string Key { get; init; }
    public required string Value { get; init; }
}
