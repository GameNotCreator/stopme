using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StopMe.App.Core.Services;
using StopMe.LocalApi.Dtos;

namespace StopMe.App.Core.ViewModels;

public partial class TemplatesViewModel : ViewModelBase
{
    private readonly IServiceConnection _connection;
    private readonly TimeProvider _clock;

    public TemplatesViewModel(IServiceConnection connection, TimeProvider clock, IDispatcher? dispatcher = null)
        : base(dispatcher)
    {
        _connection = connection;
        _clock = clock;
    }

    public ObservableCollection<TemplateDto> Templates { get; } = new();

    [ObservableProperty]
    private TemplateDto? _selectedTemplate;

    [ObservableProperty]
    private string? _lastAppliedSummary;

    // When null/null the Apply command uses "now"; once both are set we combine
    // them as a local time, then convert to UTC. CalendarDatePicker hands us a
    // DateTimeOffset (date part only); TimePicker hands us a TimeSpan.
    [ObservableProperty]
    private DateTimeOffset? _scheduledStartDate;

    [ObservableProperty]
    private TimeSpan? _scheduledStartTime;

    public bool HasNoTemplates => Templates.Count == 0;

    public bool HasScheduledStart => ScheduledStartDate is not null && ScheduledStartTime is not null;

    public string ScheduledStartLabel
    {
        get
        {
            if (!HasScheduledStart) return "Apply immediately when the button is clicked.";
            var combined = CombineScheduledLocal()!.Value;
            return $"Apply at {combined:yyyy-MM-dd HH:mm} (local).";
        }
    }

    partial void OnScheduledStartDateChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(HasScheduledStart));
        OnPropertyChanged(nameof(ScheduledStartLabel));
    }

    partial void OnScheduledStartTimeChanged(TimeSpan? value)
    {
        OnPropertyChanged(nameof(HasScheduledStart));
        OnPropertyChanged(nameof(ScheduledStartLabel));
    }

    [RelayCommand]
    public void ClearSchedule()
    {
        ScheduledStartDate = null;
        ScheduledStartTime = null;
    }

    private DateTime? CombineScheduledLocal()
    {
        if (ScheduledStartDate is null || ScheduledStartTime is null) return null;
        var d = ScheduledStartDate.Value.LocalDateTime.Date;
        return d.Add(ScheduledStartTime.Value);
    }

    public static string FormatBlocklistPreview(TemplateBlocklistDto b)
    {
        ArgumentNullException.ThrowIfNull(b);
        var parts = new List<string>();
        if (b.Categories.Count > 0) parts.Add($"{b.Categories.Count} categor{(b.Categories.Count == 1 ? "y" : "ies")}");
        if (b.Domains.Count > 0)    parts.Add($"{b.Domains.Count} domain{(b.Domains.Count == 1 ? "" : "s")}");
        if (b.ProcessNames.Count > 0) parts.Add($"{b.ProcessNames.Count} app{(b.ProcessNames.Count == 1 ? "" : "s")}");
        return parts.Count == 0 ? "(no rules)" : string.Join(", ", parts);
    }

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        ClearError();
        IsBusy = true;
        try
        {
            var list = await _connection.ListTemplatesAsync(ct).ConfigureAwait(false);
            await RunOnUiAsync(() =>
            {
                Templates.Clear();
                foreach (var t in list) Templates.Add(t);
                OnPropertyChanged(nameof(HasNoTemplates));
                if (Templates.Count > 0 && SelectedTemplate is null)
                    SelectedTemplate = Templates[0];
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsBusy = false).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task ApplyAsync(TemplateDto? template, CancellationToken ct = default)
    {
        template ??= SelectedTemplate;
        if (template is null)
        {
            SetError("Select a template first.");
            return;
        }

        ClearError();
        IsBusy = true;
        try
        {
            var startUtc = ResolveStartUtc();
            var resp = await _connection.ApplyTemplateAsync(template.Key, startUtc, ct).ConfigureAwait(false);
            if (resp is null || !resp.Success)
            {
                SetError(_connection.LastError ?? resp?.Error ?? "Could not apply template.");
                return;
            }

            await RunOnUiAsync(() =>
            {
                var when = HasScheduledStart ? $" for {startUtc:yyyy-MM-dd HH:mm} UTC" : string.Empty;
                LastAppliedSummary = $"Applied '{template.DisplayName}'{when} — session {resp.SessionId:N}, {resp.BlocklistIds.Count} blocklist(s) created.";
                StatusMessage = LastAppliedSummary;
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsBusy = false).ConfigureAwait(false);
        }
    }

    private DateTime ResolveStartUtc()
    {
        var local = CombineScheduledLocal();
        if (local is null) return _clock.GetUtcNow().UtcDateTime;
        // Treat the picker-derived value as local-time, convert to UTC.
        return DateTime.SpecifyKind(local.Value, DateTimeKind.Local).ToUniversalTime();
    }
}
