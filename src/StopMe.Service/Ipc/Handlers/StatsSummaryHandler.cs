using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;

namespace StopMe.Service.Ipc.Handlers;

public sealed class StatsSummaryHandler : IOperationHandler
{
    private readonly IUsageEventRepository _events;
    private readonly ISessionRepository _sessions;
    private readonly IBypassEventRepository _bypass;
    private readonly IUsageEventRecorder _recorder;
    private readonly IStatsAggregator _aggregator;
    private readonly TimeProvider _clock;

    public StatsSummaryHandler(
        IUsageEventRepository events,
        ISessionRepository sessions,
        IBypassEventRepository bypass,
        IUsageEventRecorder recorder,
        IStatsAggregator aggregator,
        TimeProvider clock)
    {
        _events = events;
        _sessions = sessions;
        _bypass = bypass;
        _recorder = recorder;
        _aggregator = aggregator;
        _clock = clock;
    }

    public string Op => Operations.StatsSummary;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<StatsQueryRequest>() ?? new StatsQueryRequest { Period = "Last7Days" };
        if (!Enum.TryParse<StatsPeriod>(dto.Period, ignoreCase: true, out var period))
            return LocalApiResponse.Error(ErrorCodes.BadRequest, $"Unknown period '{dto.Period}'.");

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var window = StatsWindow.From(period, nowUtc);
        var policy = await _recorder.GetActivePolicyAsync(ct).ConfigureAwait(false);

        if (policy == StatsRetentionPolicy.Disabled)
        {
            return LocalApiResponse.Ok(EmptyResponse(dto.Period, window, policy));
        }

        var fromUtc = ClampUtc(window.FromUtc);
        var toUtc = window.ToUtc;

        var usage = await _events.GetBetweenAsync(fromUtc, toUtc, ct).ConfigureAwait(false);
        var completed = await _sessions.GetCompletedBetweenAsync(fromUtc, toUtc, ct).ConfigureAwait(false);
        var bypassCount = await _bypass.CountBetweenAsync(fromUtc, toUtc, ct).ConfigureAwait(false);

        var snapshot = _aggregator.Aggregate(window, usage, completed, bypassCount, dto.TopDistractionsLimit);

        return LocalApiResponse.Ok(new StatsSummaryResponse
        {
            Period = dto.Period,
            FromUtc = window.FromUtc,
            ToUtc = window.ToUtc,
            Summary = MapSummary(snapshot.Summary),
            Daily = snapshot.Daily.Select(MapBucket).ToArray(),
            TopDistractions = snapshot.TopDistractions.Select(MapTop).ToArray(),
            PrivacyMode = policy.ToString(),
        });
    }

    private static StatsSummaryResponse EmptyResponse(string period, StatsWindow window, StatsRetentionPolicy policy) =>
        new()
        {
            Period = period,
            FromUtc = window.FromUtc,
            ToUtc = window.ToUtc,
            Summary = new StatsSummaryDto
            {
                SessionsCompleted = 0,
                FocusMinutes = 0,
                WebsiteBlockCount = 0,
                AppBlockCount = 0,
                BypassAttemptCount = 0,
                ProductiveDayStreak = 0,
            },
            Daily = Array.Empty<StatsDailyBucketDto>(),
            TopDistractions = Array.Empty<StatsTopDistractionDto>(),
            PrivacyMode = policy.ToString(),
        };

    private static StatsSummaryDto MapSummary(StatsSummary s) => new()
    {
        SessionsCompleted = s.SessionsCompleted,
        FocusMinutes = s.FocusMinutes,
        WebsiteBlockCount = s.WebsiteBlockCount,
        AppBlockCount = s.AppBlockCount,
        BypassAttemptCount = s.BypassAttemptCount,
        ProductiveDayStreak = s.ProductiveDayStreak,
    };

    private static StatsDailyBucketDto MapBucket(DailyBucket b) => new()
    {
        Date = b.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        FocusMinutes = b.FocusMinutes,
        WebsiteBlockCount = b.WebsiteBlockCount,
        AppBlockCount = b.AppBlockCount,
        SessionsCompleted = b.SessionsCompleted,
    };

    private static StatsTopDistractionDto MapTop(TopDistraction t) => new()
    {
        Label = t.Label,
        BlockCount = t.BlockCount,
        Kind = t.Kind,
    };

    private static DateTime ClampUtc(DateTime utc)
    {
        // SQLite stores DateTime as text. DateTime.MinValue.ToUniversalTime() rounds to a
        // value that round-trips fine, but Sqlite-EF compares fail on it on some platforms.
        // Clamp to the unix epoch lower bound for "AllTime" queries.
        return utc < new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            : utc;
    }
}
