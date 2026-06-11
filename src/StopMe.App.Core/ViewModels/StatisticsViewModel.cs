using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StopMe.App.Core.Services;
using StopMe.LocalApi.Dtos;

namespace StopMe.App.Core.ViewModels;

public partial class StatisticsViewModel : ViewModelBase
{
    public static readonly string[] AvailablePeriods = { "Today", "Last7Days", "Last30Days", "AllTime" };
    public static readonly string[] AvailablePrivacyModes = { "Readable", "Anonymized", "Disabled" };

    private readonly IServiceConnection _connection;
    private readonly IStatsCsvWriter _csv;

    public StatisticsViewModel(IServiceConnection connection, IStatsCsvWriter csv)
    {
        _connection = connection;
        _csv = csv;
        _selectedPeriod = "Last7Days";
        _selectedPrivacyMode = "Readable";
    }

    public ObservableCollection<StatsDailyBucketDto> Daily { get; } = new();

    public ObservableCollection<StatsTopDistractionDto> TopDistractions { get; } = new();

    public IReadOnlyList<string> AvailablePeriodList { get; } = AvailablePeriods;

    public IReadOnlyList<string> AvailablePrivacyModeList { get; } = AvailablePrivacyModes;

    [ObservableProperty]
    private string _selectedPeriod;

    [ObservableProperty]
    private string _selectedPrivacyMode;

    [ObservableProperty]
    private int _sessionsCompleted;

    [ObservableProperty]
    private int _focusMinutes;

    [ObservableProperty]
    private int _websiteBlockCount;

    [ObservableProperty]
    private int _appBlockCount;

    [ObservableProperty]
    private int _bypassAttemptCount;

    [ObservableProperty]
    private int _productiveDayStreak;

    [ObservableProperty]
    private DateTime _windowFromUtc;

    [ObservableProperty]
    private DateTime _windowToUtc;

    [ObservableProperty]
    private string? _lastExportPath;

    [ObservableProperty]
    private string? _lastResetSummary;

    public string SelectedPeriodDescription => SelectedPeriod switch
    {
        "Today" => "Today",
        "Last7Days" => "Last 7 days",
        "Last30Days" => "Last 30 days",
        "AllTime" => "All time",
        _ => SelectedPeriod,
    };

    partial void OnSelectedPeriodChanged(string value) =>
        OnPropertyChanged(nameof(SelectedPeriodDescription));

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        ClearError();
        IsBusy = true;
        try
        {
            await LoadStatsAsync(ct).ConfigureAwait(false);
            await LoadPrivacyAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ApplyPrivacyAsync(CancellationToken ct = default)
    {
        ClearError();
        var ok = await _connection.SetSettingAsync("stats.privacy_mode", SelectedPrivacyMode, ct).ConfigureAwait(false);
        if (!ok)
        {
            SetError("Could not update privacy mode.");
            return;
        }
        StatusMessage = $"Privacy mode set to {SelectedPrivacyMode}.";
        await LoadStatsAsync(ct).ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task ExportCsvAsync(CancellationToken ct = default)
    {
        ClearError();
        var resp = await _connection.ExportStatsCsvAsync(SelectedPeriod, ct).ConfigureAwait(false);
        if (resp is null)
        {
            SetError("Could not export statistics.");
            return;
        }
        var path = _csv.SaveCsv(resp.Filename, resp.CsvText);
        LastExportPath = path;
        StatusMessage = $"Exported to {path}.";
    }

    [RelayCommand]
    public async Task ResetAsync(CancellationToken ct = default)
    {
        ClearError();
        var deleted = await _connection.ResetStatsAsync(ct).ConfigureAwait(false);
        LastResetSummary = $"Deleted {deleted} usage event(s).";
        StatusMessage = LastResetSummary;
        await LoadStatsAsync(ct).ConfigureAwait(false);
    }

    private async Task LoadStatsAsync(CancellationToken ct)
    {
        var resp = await _connection.GetStatsAsync(SelectedPeriod, ct).ConfigureAwait(false);
        if (resp is null)
        {
            SetError(_connection.LastError ?? "Service is unreachable.");
            Daily.Clear();
            TopDistractions.Clear();
            return;
        }

        SessionsCompleted = resp.Summary.SessionsCompleted;
        FocusMinutes = resp.Summary.FocusMinutes;
        WebsiteBlockCount = resp.Summary.WebsiteBlockCount;
        AppBlockCount = resp.Summary.AppBlockCount;
        BypassAttemptCount = resp.Summary.BypassAttemptCount;
        ProductiveDayStreak = resp.Summary.ProductiveDayStreak;
        WindowFromUtc = resp.FromUtc;
        WindowToUtc = resp.ToUtc;

        Daily.Clear();
        foreach (var b in resp.Daily) Daily.Add(b);

        TopDistractions.Clear();
        foreach (var t in resp.TopDistractions) TopDistractions.Add(t);

        if (!string.Equals(resp.PrivacyMode, SelectedPrivacyMode, StringComparison.OrdinalIgnoreCase))
            SelectedPrivacyMode = resp.PrivacyMode;
    }

    private async Task LoadPrivacyAsync(CancellationToken ct)
    {
        var entries = await _connection.GetSettingsAsync(ct).ConfigureAwait(false);
        if (entries.TryGetValue("stats.privacy_mode", out var policy)
            && AvailablePrivacyModes.Contains(policy, StringComparer.OrdinalIgnoreCase))
        {
            SelectedPrivacyMode = policy;
        }
    }
}
