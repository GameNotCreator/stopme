using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StopMe.App.Core.Services;
using StopMe.LocalApi.Dtos;

namespace StopMe.App.Core.ViewModels;

// One-stop "block these apps now" page. Lists the running apps the service
// reports, lets the user check the ones to block, picks a duration, and
// creates+starts a one-shot session backed by a fresh blocklist of
// WindowsProcessName rules. End-to-end: this is the spec's central use case.
public partial class BlockAppsViewModel : ViewModelBase
{
    public static readonly int[] AvailableDurationsMinutes = { 15, 30, 60, 120, 240 };
    public static readonly string[] AvailableModes = { "Soft", "Strict" };

    private readonly IServiceConnection _connection;
    private readonly TimeProvider _clock;

    public BlockAppsViewModel(IServiceConnection connection, TimeProvider clock)
    {
        _connection = connection;
        _clock = clock;
        _durationMinutes = 30;
        _selectedMode = "Soft";
        _manualProcessName = string.Empty;
        _sessionName = "Quick block";
    }

    public ObservableCollection<RunningAppItem> RunningApps { get; } = new();
    public ObservableCollection<InstalledAppItem> InstalledApps { get; } = new();
    public ObservableCollection<ManualAppItem> ManualEntries { get; } = new();

    public IReadOnlyList<int> AvailableDurations { get; } = AvailableDurationsMinutes;
    public IReadOnlyList<string> AvailableModeList { get; } = AvailableModes;

    [ObservableProperty]
    private int _durationMinutes;

    [ObservableProperty]
    private string _selectedMode;

    [ObservableProperty]
    private string _manualProcessName;

    [ObservableProperty]
    private string _sessionName;

    [ObservableProperty]
    private DateTime _lastSnapshotUtc;

    [ObservableProperty]
    private string? _lastStartSummary;

    public int SelectedCount =>
        RunningApps.Count(a => a.IsSelected)
        + InstalledApps.Count(a => a.IsSelected)
        + ManualEntries.Count(m => !string.IsNullOrWhiteSpace(m.ProcessName));

    public bool HasSelection => SelectedCount > 0;

    public bool HasNoApps => RunningApps.Count == 0;

    public string DurationLabel => DurationMinutes switch
    {
        < 60 => $"{DurationMinutes} minutes",
        60 => "1 hour",
        120 => "2 hours",
        240 => "4 hours",
        _ => $"{DurationMinutes} minutes",
    };

    partial void OnDurationMinutesChanged(int value) =>
        OnPropertyChanged(nameof(DurationLabel));

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        ClearError();
        IsBusy = true;
        try
        {
            // ---- Running apps ----
            var running = await _connection.GetRunningAppsAsync(ct).ConfigureAwait(false);

            // Preserve current checkbox state across refreshes.
            var previouslySelected = RunningApps
                .Where(a => a.IsSelected)
                .Select(a => a.ProcessName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            RunningApps.Clear();
            foreach (var a in running)
            {
                RunningApps.Add(new RunningAppItem
                {
                    ProcessName = a.ProcessName,
                    DisplayLabel = string.IsNullOrEmpty(a.MainWindowTitle)
                        ? a.ProcessName
                        : $"{a.ProcessName}  —  {a.MainWindowTitle}",
                    ExecutablePath = a.ExecutablePath,
                    HasVisibleWindow = a.HasVisibleWindow,
                    IsSelected = previouslySelected.Contains(a.ProcessName),
                });
            }

            // ---- Installed apps (Registry + Start Menu) ----
            var installed = await _connection.GetInstalledAppsAsync(ct).ConfigureAwait(false);
            var prevInstalledSelected = InstalledApps
                .Where(a => a.IsSelected)
                .Select(a => a.ProcessName ?? a.DisplayName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            InstalledApps.Clear();
            foreach (var a in installed)
            {
                if (string.IsNullOrEmpty(a.ProcessName)) continue; // we need a process name to block on
                InstalledApps.Add(new InstalledAppItem
                {
                    DisplayName = a.DisplayName,
                    Publisher = a.Publisher,
                    ProcessName = a.ProcessName,
                    ExecutablePath = a.ExecutablePath,
                    Source = a.Source,
                    IsSelected = prevInstalledSelected.Contains(a.ProcessName!) || prevInstalledSelected.Contains(a.DisplayName),
                });
            }

            LastSnapshotUtc = _clock.GetUtcNow().UtcDateTime;
            OnPropertyChanged(nameof(HasNoApps));
            OnPropertyChanged(nameof(HasNoInstalled));
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
        }
        finally
        {
            IsBusy = false;
        }
    }

    public bool HasNoInstalled => InstalledApps.Count == 0;

    [RelayCommand]
    public void ToggleInstalled(InstalledAppItem item)
    {
        if (item is null) return;
        item.IsSelected = !item.IsSelected;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    public void AddManualEntry()
    {
        var name = ManualProcessName?.Trim();
        if (string.IsNullOrEmpty(name)) return;
        if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name += ".exe";
        if (ManualEntries.Any(m => string.Equals(m.ProcessName, name, StringComparison.OrdinalIgnoreCase))) return;

        ManualEntries.Add(new ManualAppItem { ProcessName = name });
        ManualProcessName = string.Empty;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    public void RemoveManualEntry(ManualAppItem item)
    {
        if (item is null) return;
        ManualEntries.Remove(item);
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    public void ToggleSelection(RunningAppItem item)
    {
        if (item is null) return;
        item.IsSelected = !item.IsSelected;
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    public async Task StartBlockingAsync(CancellationToken ct = default)
    {
        ClearError();

        var selectedNames = RunningApps.Where(a => a.IsSelected).Select(a => a.ProcessName)
            .Concat(InstalledApps.Where(a => a.IsSelected).Select(a => a.ProcessName ?? a.DisplayName))
            .Concat(ManualEntries.Select(m => m.ProcessName))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n : n + ".exe")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (selectedNames.Length == 0)
        {
            SetError("Select at least one app (or add one manually) before starting.");
            return;
        }

        if (DurationMinutes <= 0)
        {
            SetError("Pick a positive duration.");
            return;
        }

        IsBusy = true;
        try
        {
            var blocklist = await _connection.CreateBlocklistAsync(new CreateBlocklistRequest
            {
                Name = $"Quick block — {_clock.GetUtcNow().UtcDateTime:yyyy-MM-dd HH:mm}",
                Description = $"Auto-generated for: {string.Join(", ", selectedNames)}",
                Mode = "Blacklist",
                Rules = selectedNames.Select(n => new CreateRuleRequest
                {
                    Type = "WindowsProcessName",
                    Value = n,
                }).ToArray(),
            }, ct).ConfigureAwait(false);

            if (blocklist is null)
            {
                SetError("Could not create blocklist. " + (_connection.LastError ?? string.Empty));
                return;
            }

            var startAtUtc = _clock.GetUtcNow().UtcDateTime;
            var endAtUtc = startAtUtc.AddMinutes(DurationMinutes);

            var lockPolicy = string.Equals(SelectedMode, "Strict", StringComparison.OrdinalIgnoreCase)
                ? "TimeLocked"
                : "Confirmation";

            var session = await _connection.CreateSessionAsync(new CreateSessionRequest
            {
                Name = string.IsNullOrWhiteSpace(SessionName) ? "Quick block" : SessionName.Trim(),
                StartAtUtc = startAtUtc,
                EndAtUtc = endAtUtc,
                Mode = SelectedMode,
                LockPolicy = lockPolicy,
                BlocklistIds = new[] { blocklist.Id },
                StartImmediately = true,
            }, ct).ConfigureAwait(false);

            if (session is null)
            {
                SetError("Blocklist created but session could not start. " + (_connection.LastError ?? string.Empty));
                return;
            }

            LastStartSummary = session.Started
                ? $"Blocking {selectedNames.Length} app(s) for {DurationLabel}. Ends at {endAtUtc.ToLocalTime():HH:mm}."
                : $"Session scheduled but not started: {session.Warning}";
            StatusMessage = LastStartSummary;

            // Clear manual entries to avoid carrying them across blocks; keep running-app
            // selections so the user can immediately re-block the same set if they want.
            ManualEntries.Clear();
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed partial class RunningAppItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public required string ProcessName { get; init; }
    public required string DisplayLabel { get; init; }
    public string? ExecutablePath { get; init; }
    public bool HasVisibleWindow { get; init; }
}

public sealed partial class InstalledAppItem : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public required string DisplayName { get; init; }
    public string? Publisher { get; init; }
    public string? ProcessName { get; init; }
    public string? ExecutablePath { get; init; }
    public required string Source { get; init; }
}

public sealed class ManualAppItem
{
    public required string ProcessName { get; init; }
}
