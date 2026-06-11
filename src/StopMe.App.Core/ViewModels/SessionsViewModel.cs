using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StopMe.App.Core.Services;
using StopMe.LocalApi.Dtos;

namespace StopMe.App.Core.ViewModels;

// Sessions page — surfaces every session bucket in one place:
//   Active     — the currently running session (or moon empty-state)
//   Scheduled  — sessions with StartAtUtc in the future
//   Recurring  — RRULE-backed sessions (with enable toggle)
//   Past       — sessions that already finished / were stopped early
//
// Backed by the existing service IPC: GetActiveSessionsAsync gives us "Active",
// and the persisted FocusSession table will eventually back "Scheduled / Recurring /
// Past" once a sessions.list IPC op exists. Until then those three are sourced
// from the demo bucket the prototype ships with so the layout still renders
// correctly; the empty paths handle that gracefully.
public partial class SessionsViewModel : ViewModelBase
{
    private readonly IServiceConnection _connection;

    public SessionsViewModel(IServiceConnection connection)
    {
        _connection = connection;
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
            var sessions = await _connection.GetActiveSessionsAsync(ct).ConfigureAwait(false);
            Active.Clear();
            foreach (var s in sessions) Active.Add(s);

            // TODO: replace these prototype demo rows with the real persisted
            // sessions once a sessions.list IPC op exists. The Phase 1 FocusSession
            // table already stores everything we need (StartAtUtc, RecurrenceRule,
            // Status); only the read-path is missing on the wire.
            if (Scheduled.Count == 0)
            {
                Scheduled.Add(new ScheduledSessionRow("Evening study", "Today, 7:00 PM", 90, true, 5, 10));
            }
            if (Recurring.Count == 0)
            {
                Recurring.Add(new RecurringSessionRow("Work mornings", new[] { "Mon", "Tue", "Wed", "Thu", "Fri" }, "9:00 AM – 12:00 PM", false, 4, 12, Enabled: true));
                Recurring.Add(new RecurringSessionRow("No late-night gaming", new[] { "Every day" }, "11:00 PM – 7:00 AM", true, 3, 4, Enabled: false));
            }
            if (Past.Count == 0)
            {
                Past.Add(new PastSessionRow("Deep work",       "Today, 9:00 AM",     120, Completed: true,  StoppedAtMins: 120, Strict: true,  Apps: 4, Sites: 12));
                Past.Add(new PastSessionRow("Morning focus",   "Yesterday, 8:30 AM",  90, Completed: true,  StoppedAtMins:  90, Strict: false, Apps: 3, Sites: 8));
                Past.Add(new PastSessionRow("Exam revision",   "Mon, 2:00 PM",       180, Completed: false, StoppedAtMins: 124, Strict: false, Apps: 6, Sites: 15));
                Past.Add(new PastSessionRow("Deep work",       "Mon, 9:00 AM",       120, Completed: true,  StoppedAtMins: 120, Strict: true,  Apps: 4, Sites: 12));
            }

            OnPropertyChanged(nameof(HasActive));
            OnPropertyChanged(nameof(HasNoActive));
            OnPropertyChanged(nameof(HasScheduled));
            OnPropertyChanged(nameof(HasRecurring));
            OnPropertyChanged(nameof(HasPast));
        }
        finally
        {
            IsBusy = false;
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
            Active.Remove(current);
            OnPropertyChanged(nameof(HasActive));
            OnPropertyChanged(nameof(HasNoActive));
            StatusMessage = "Session stopped.";
        }
        else
        {
            SetError(resp?.DenialReason ?? _connection.LastError ?? "Could not stop session.");
        }
    }
}

public sealed record ScheduledSessionRow(string Name, string When, int DurationMinutes, bool Strict, int Apps, int Sites);

public sealed record RecurringSessionRow(string Name, IReadOnlyList<string> Days, string Time, bool Strict, int Apps, int Sites, bool Enabled);

public sealed record PastSessionRow(string Name, string Date, int DurationMinutes, bool Completed, int StoppedAtMins, bool Strict, int Apps, int Sites);
