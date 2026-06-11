using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Core.ValueObjects;

namespace StopMe.Core.Abstractions;

public interface IStatsAggregator
{
    StatsSnapshot Aggregate(
        StatsWindow window,
        IReadOnlyList<UsageEvent> usageEvents,
        IReadOnlyList<FocusSession> completedSessions,
        int bypassCount,
        int topDistractionsLimit = 5);
}
