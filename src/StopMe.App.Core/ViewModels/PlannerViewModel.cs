using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StopMe.App.Core.Services;
using StopMe.LocalApi.Dtos;

namespace StopMe.App.Core.ViewModels;

// Real session-creation form (was a 7-line stub). Lets the user assemble a
// FocusSession from existing blocklists, pick a duration, pick a lock policy,
// then create + optionally start it. Talks to sessions.create on the service.
public partial class PlannerViewModel : ViewModelBase
{
    public static readonly int[] AvailableDurationsMinutes = { 15, 30, 60, 90, 120, 180, 240, 480 };
    public static readonly string[] AvailableModes = { "Soft", "Strict" };
    public static readonly string[] AvailableLockPolicies =
    {
        "None", "Confirmation", "RandomText", "Password", "TimeLocked", "RestartRequired",
    };

    private readonly IServiceConnection _connection;
    private readonly TimeProvider _clock;

    public PlannerViewModel(IServiceConnection connection, TimeProvider clock, IDispatcher? dispatcher = null)
        : base(dispatcher)
    {
        _connection = connection;
        _clock = clock;
        _sessionName = "Focus block";
        _durationMinutes = 60;
        _selectedMode = "Soft";
        _selectedLockPolicy = "Confirmation";
        _requiredText = string.Empty;
        _password = string.Empty;
        _useScheduledStart = false;
    }

    public ObservableCollection<BlocklistChoice> Blocklists { get; } = new();

    public IReadOnlyList<int> AvailableDurations { get; } = AvailableDurationsMinutes;
    public IReadOnlyList<string> AvailableModeList { get; } = AvailableModes;
    public IReadOnlyList<string> AvailableLockPolicyList { get; } = AvailableLockPolicies;

    [ObservableProperty]
    private string _sessionName;

    [ObservableProperty]
    private int _durationMinutes;

    [ObservableProperty]
    private string _selectedMode;

    [ObservableProperty]
    private string _selectedLockPolicy;

    [ObservableProperty]
    private string _requiredText;

    [ObservableProperty]
    private string _password;

    [ObservableProperty]
    private bool _useScheduledStart;

    [ObservableProperty]
    private DateTimeOffset? _scheduledStartDate;

    [ObservableProperty]
    private TimeSpan? _scheduledStartTime;

    [ObservableProperty]
    private string? _lastCreatedSummary;

    public bool ShowsRequiredTextField => string.Equals(SelectedLockPolicy, "RandomText", StringComparison.OrdinalIgnoreCase);
    public bool ShowsPasswordField => string.Equals(SelectedLockPolicy, "Password", StringComparison.OrdinalIgnoreCase);

    partial void OnSelectedLockPolicyChanged(string value)
    {
        OnPropertyChanged(nameof(ShowsRequiredTextField));
        OnPropertyChanged(nameof(ShowsPasswordField));
    }

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        ClearError();
        IsBusy = true;
        try
        {
            var list = await _connection.ListBlocklistsAsync(ct).ConfigureAwait(false);
            await RunOnUiAsync(() =>
            {
                var previouslyChecked = Blocklists.Where(b => b.IsSelected).Select(b => b.Id).ToHashSet();
                Blocklists.Clear();
                foreach (var b in list)
                {
                    Blocklists.Add(new BlocklistChoice
                    {
                        Id = b.Id,
                        Name = b.Name,
                        Mode = b.Mode,
                        RuleCount = b.Rules.Count,
                        IsSelected = previouslyChecked.Contains(b.Id),
                    });
                }
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsBusy = false).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task CreateAndStartAsync(CancellationToken ct = default) =>
        await CreateAsync(startImmediately: true, ct).ConfigureAwait(false);

    [RelayCommand]
    public async Task CreateOnlyAsync(CancellationToken ct = default) =>
        await CreateAsync(startImmediately: false, ct).ConfigureAwait(false);

    private async Task CreateAsync(bool startImmediately, CancellationToken ct)
    {
        ClearError();

        if (string.IsNullOrWhiteSpace(SessionName))
        {
            SetError("Give the session a name.");
            return;
        }
        if (DurationMinutes <= 0)
        {
            SetError("Pick a positive duration.");
            return;
        }
        if (string.Equals(SelectedLockPolicy, "RandomText", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(RequiredText))
        {
            SetError("RandomText lock requires a phrase the user will need to type to stop the session.");
            return;
        }
        if (string.Equals(SelectedLockPolicy, "Password", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(Password))
        {
            SetError("Password lock requires a password.");
            return;
        }

        var selectedBlocklistIds = Blocklists.Where(b => b.IsSelected).Select(b => b.Id).ToArray();
        if (selectedBlocklistIds.Length == 0)
        {
            SetError("Select at least one blocklist to apply.");
            return;
        }

        var startUtc = ResolveStartUtc(out var explicitlyScheduled);
        var endUtc = startUtc.AddMinutes(DurationMinutes);

        IsBusy = true;
        try
        {
            var resp = await _connection.CreateSessionAsync(new CreateSessionRequest
            {
                Name = SessionName.Trim(),
                StartAtUtc = startUtc,
                EndAtUtc = endUtc,
                Mode = SelectedMode,
                LockPolicy = SelectedLockPolicy,
                BlocklistIds = selectedBlocklistIds,
                RequiredText = ShowsRequiredTextField ? RequiredText : null,
                Password = ShowsPasswordField ? Password : null,
                StartImmediately = startImmediately && !explicitlyScheduled,
            }, ct).ConfigureAwait(false);

            if (resp is null)
            {
                SetError(_connection.LastError ?? "Could not create the session.");
                return;
            }

            await RunOnUiAsync(() =>
            {
                LastCreatedSummary = resp.Started
                    ? $"Session '{SessionName.Trim()}' started — ends at {endUtc.ToLocalTime():yyyy-MM-dd HH:mm}."
                    : explicitlyScheduled
                        ? $"Session '{SessionName.Trim()}' scheduled for {startUtc.ToLocalTime():yyyy-MM-dd HH:mm}."
                        : (resp.Warning ?? "Session created.");
                StatusMessage = LastCreatedSummary;
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsBusy = false).ConfigureAwait(false);
        }
    }

    private DateTime ResolveStartUtc(out bool scheduled)
    {
        if (UseScheduledStart && ScheduledStartDate is { } d && ScheduledStartTime is { } t)
        {
            scheduled = true;
            var local = DateTime.SpecifyKind(d.LocalDateTime.Date.Add(t), DateTimeKind.Local);
            return local.ToUniversalTime();
        }
        scheduled = false;
        return _clock.GetUtcNow().UtcDateTime;
    }
}

public sealed partial class BlocklistChoice : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Mode { get; init; }
    public int RuleCount { get; init; }
}
