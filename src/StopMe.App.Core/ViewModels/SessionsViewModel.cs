using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StopMe.App.Core.Services;
using StopMe.LocalApi.Dtos;

namespace StopMe.App.Core.ViewModels;

// Sessions page — surfaces every session bucket in one place:
//   Active     — the currently running session (or moon empty-state)
//   Scheduled  — sessions with StartAtUtc in the future
//   Recurring  — RRULE-backed sessions
//   Past       — sessions that already finished / were stopped early
//
// Backed by `sessions.active` for the Active bucket and the new `sessions.list`
// IPC op for Scheduled / Recurring / Past. The prototype demo rows are gone —
// what the user sees here is what is in the FocusSession table.
public partial class SessionsViewModel : ViewModelBase
{
    private readonly IServiceConnection _connection;
    private readonly TimeProvider _clock;

    public SessionsViewModel(IServiceConnection connection, TimeProvider clock, IDispatcher? dispatcher = null)
        : base(dispatcher)
    {
        _connection = connection;
        _clock = clock;
    }

    public ObservableCollection<ActiveSessionDto> Active { get; } = new();

    public ObservableCollection<ScheduledSessionRow> Scheduled { get; } = new();

    public ObservableCollection<RecurringSessionRow> Recurring { get; } = new();

    public ObservableCollection<PastSessionRow> Past { get; } = new();

    public bool HasActive => Active.Count > 0;
    public bool HasNoActive => Active.Count == 0;
    public bool HasScheduled => Scheduled.Count > 0;
    public bool HasRecurring => Recurring.Count > 0;
    public bool HasPast => Past.Count > 0;

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        ClearError();
        IsBusy = true;
        try
        {
            var nowUtc = _clock.GetUtcNow().UtcDateTime;
            var activeDtos = await _connection.GetActiveSessionsAsync(ct).ConfigureAwait(false);
            var listed = await _connection.GetSessionListAsync(statusFilter: null, ct).ConfigureAwait(false);

            var scheduledRows = listed
                .Where(s => !s.IsRecurring)
                .Where(s => string.Equals(s.Status, "Scheduled", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase))
                .Where(s => s.StartAtUtc > nowUtc)
                .OrderBy(s => s.StartAtUtc)
                .Select(s => new ScheduledSessionRow(
                    s.Name,
                    s.StartAtUtc.ToLocalTime().ToString("ddd, t"),
                    (int)Math.Max(0, (s.EndAtUtc - s.StartAtUtc).TotalMinutes),
                    string.Equals(s.Mode, "Strict", StringComparison.OrdinalIgnoreCase),
                    Apps: 0,
                    Sites: 0))
                .ToList();

            var recurringRows = listed
                .Where(s => s.IsRecurring)
                .OrderBy(s => s.Name)
                .Select(s => new RecurringSessionRow(
                    s.Name,
                    SummariseRecurrence(s.RecurrenceRule),
                    $"{s.StartAtUtc.ToLocalTime():t} – {s.EndAtUtc.ToLocalTime():t}",
                    string.Equals(s.Mode, "Strict", StringComparison.OrdinalIgnoreCase),
                    Apps: 0,
                    Sites: 0,
                    Enabled: !string.Equals(s.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var pastRows = listed
                .Where(s => !s.IsRecurring)
                .Where(s => string.Equals(s.Status, "Completed", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(s.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(s.Status, "Failed", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.EndAtUtc)
                .Take(20)
                .Select(s =>
                {
                    var dur = (int)Math.Max(0, (s.EndAtUtc - s.StartAtUtc).TotalMinutes);
                    return new PastSessionRow(
                        s.Name,
                        s.StartAtUtc.ToLocalTime().ToString("ddd, t"),
                        dur,
                        Completed: string.Equals(s.Status, "Completed", StringComparison.OrdinalIgnoreCase),
                        StoppedAtMins: dur,
                        Strict: string.Equals(s.Mode, "Strict", StringComparison.OrdinalIgnoreCase),
                        Apps: 0,
                        Sites: 0);
                })
                .ToList();

            await RunOnUiAsync(() =>
            {
                Active.Clear();
                foreach (var s in activeDtos) Active.Add(s);

                Scheduled.Clear();
                foreach (var r in scheduledRows) Scheduled.Add(r);

                Recurring.Clear();
                foreach (var r in recurringRows) Recurring.Add(r);

                Past.Clear();
                foreach (var r in pastRows) Past.Add(r);

                OnPropertyChanged(nameof(HasActive));
                OnPropertyChanged(nameof(HasNoActive));
                OnPropertyChanged(nameof(HasScheduled));
                OnPropertyChanged(nameof(HasRecurring));
                OnPropertyChanged(nameof(HasPast));
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsBusy = false).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task StopActiveAsync(CancellationToken ct = default)
    {
        var current = Active.FirstOrDefault();
        if (current is null) return;
        var resp = await _connection.StopSessionAsync(new StopSessionRequest
        {
            SessionId = current.SessionId,
            Reason = "UserRequested",
            UserConfirmed = true,
        }, ct).ConfigureAwait(false);
        if (resp?.Stopped == true)
        {
            await RunOnUiAsync(() =>
            {
                Active.Remove(current);
                OnPropertyChanged(nameof(HasActive));
                OnPropertyChanged(nameof(HasNoActive));
                StatusMessage = "Session stopped.";
            }).ConfigureAwait(false);
        }
        else
        {
            await RunOnUiAsync(() =>
                SetError(resp?.DenialReason ?? _connection.LastError ?? "Could not stop session.")
            ).ConfigureAwait(false);
        }
    }

    private static string SummariseRecurrence(string? rrule)
    {
        if (string.IsNullOrWhiteSpace(rrule)) return "—";
        // Tiny RRULE summary; the full parser is in StopMe.Core.Recurrence and
        // we deliberately don't share its logic here just to render a chip.
        var byday = ExtractTag(rrule, "BYDAY=");
        if (!string.IsNullOrEmpty(byday)) return byday;
        if (rrule.Contains("FREQ=DAILY", StringComparison.OrdinalIgnoreCase)) return "Every day";
        if (rrule.Contains("FREQ=WEEKLY", StringComparison.OrdinalIgnoreCase)) return "Weekly";
        return rrule;
    }

    private static string? ExtractTag(string rrule, string tag)
    {
        var idx = rrule.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var rest = rrule[(idx + tag.Length)..];
        var end = rest.IndexOf(';');
        return end < 0 ? rest : rest[..end];
    }
}

public sealed record ScheduledSessionRow(string Name, string When, int DurationMinutes, bool Strict, int Apps, int Sites);

public sealed record RecurringSessionRow(string Name, string Days, string Time, bool Strict, int Apps, int Sites, bool Enabled);

public sealed record PastSessionRow(string Name, string Date, int DurationMinutes, bool Completed, int StoppedAtMins, bool Strict, int Apps, int Sites);
