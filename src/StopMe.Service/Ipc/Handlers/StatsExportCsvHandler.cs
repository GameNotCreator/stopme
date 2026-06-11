using System.Globalization;
using System.Text;
using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;

namespace StopMe.Service.Ipc.Handlers;

// Returns the per-day buckets + top distractions of a window as CSV. The UI
// writes the response body to disk; the service never touches the user's
// filesystem on its own.
public sealed class StatsExportCsvHandler : IOperationHandler
{
    private readonly IUsageEventRepository _events;
    private readonly ISessionRepository _sessions;
    private readonly IBypassEventRepository _bypass;
    private readonly IUsageEventRecorder _recorder;
    private readonly IStatsAggregator _aggregator;
    private readonly TimeProvider _clock;

    public StatsExportCsvHandler(
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

    public string Op => Operations.StatsExportCsv;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<StatsExportRequest>() ?? new StatsExportRequest { Period = "Last30Days" };
        if (!Enum.TryParse<StatsPeriod>(dto.Period, ignoreCase: true, out var period))
            return LocalApiResponse.Error(ErrorCodes.BadRequest, $"Unknown period '{dto.Period}'.");

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var window = StatsWindow.From(period, nowUtc);
        var policy = await _recorder.GetActivePolicyAsync(ct).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# StopMe statistics export ({dto.Period})");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Generated: {nowUtc:O}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"# Privacy mode: {policy}");

        if (policy == StatsRetentionPolicy.Disabled)
        {
            sb.AppendLine("# Stats collection is currently disabled — export is empty.");
            return LocalApiResponse.Ok(new StatsExportResponse
            {
                Filename = BuildFilename(dto.Period, nowUtc),
                CsvText = sb.ToString(),
            });
        }

        var fromUtc = window.FromUtc < new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            : window.FromUtc;
        var toUtc = window.ToUtc;

        var usage = await _events.GetBetweenAsync(fromUtc, toUtc, ct).ConfigureAwait(false);
        var completed = await _sessions.GetCompletedBetweenAsync(fromUtc, toUtc, ct).ConfigureAwait(false);
        var bypassCount = await _bypass.CountBetweenAsync(fromUtc, toUtc, ct).ConfigureAwait(false);

        var snapshot = _aggregator.Aggregate(window, usage, completed, bypassCount, topDistractionsLimit: 20);

        sb.AppendLine();
        sb.AppendLine("section,date,focus_minutes,website_blocks,app_blocks,sessions_completed");
        foreach (var b in snapshot.Daily)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"daily,{b.Date:yyyy-MM-dd},{b.FocusMinutes},{b.WebsiteBlockCount},{b.AppBlockCount},{b.SessionsCompleted}");
        }

        sb.AppendLine();
        sb.AppendLine("section,kind,label,block_count");
        foreach (var t in snapshot.TopDistractions)
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"top,{Escape(t.Kind)},{Escape(t.Label)},{t.BlockCount}");
        }

        sb.AppendLine();
        sb.AppendLine("section,sessions_completed,focus_minutes,website_blocks,app_blocks,bypass_attempts,productive_streak");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"summary,{snapshot.Summary.SessionsCompleted},{snapshot.Summary.FocusMinutes},{snapshot.Summary.WebsiteBlockCount},{snapshot.Summary.AppBlockCount},{snapshot.Summary.BypassAttemptCount},{snapshot.Summary.ProductiveDayStreak}");

        return LocalApiResponse.Ok(new StatsExportResponse
        {
            Filename = BuildFilename(dto.Period, nowUtc),
            CsvText = sb.ToString(),
        });
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static string BuildFilename(string period, DateTime nowUtc) =>
        $"stopme-stats-{period.ToLowerInvariant()}-{nowUtc:yyyyMMdd-HHmmss}.csv";
}
