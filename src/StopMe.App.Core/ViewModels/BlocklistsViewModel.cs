using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StopMe.App.Core.Services;
using StopMe.LocalApi.Dtos;

namespace StopMe.App.Core.ViewModels;

public partial class BlocklistsViewModel : ViewModelBase
{
    private readonly IServiceConnection _connection;

    public BlocklistsViewModel(IServiceConnection connection, IDispatcher? dispatcher = null)
        : base(dispatcher)
    {
        _connection = connection;
        Modes = new[] { "Blacklist", "Allowlist", "FullInternet" };
        SelectedMode = Modes[0];
    }

    public ObservableCollection<BlocklistDto> Blocklists { get; } = new();

    public IReadOnlyList<string> Modes { get; }

    [ObservableProperty]
    private BlocklistDto? _selectedBlocklist;

    [ObservableProperty]
    private string _newName = string.Empty;

    [ObservableProperty]
    private string _selectedMode;

    [ObservableProperty]
    private string _newRulesText = string.Empty;

    public bool HasSelection => SelectedBlocklist is not null;

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        ClearError();
        IsBusy = true;
        try
        {
            var lists = await _connection.ListBlocklistsAsync(ct).ConfigureAwait(false);
            await RunOnUiAsync(() =>
            {
                Blocklists.Clear();
                foreach (var b in lists) Blocklists.Add(b);
                if (Blocklists.Count > 0 && SelectedBlocklist is null)
                    SelectedBlocklist = Blocklists[0];
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsBusy = false).ConfigureAwait(false);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    public async Task CreateAsync(CancellationToken ct = default)
    {
        ClearError();
        if (string.IsNullOrWhiteSpace(NewName)) return;

        IsBusy = true;
        try
        {
            var rules = ParseRules(NewRulesText);
            var created = await _connection.CreateBlocklistAsync(new CreateBlocklistRequest
            {
                Name = NewName.Trim(),
                Mode = SelectedMode,
                Rules = rules,
            }, ct).ConfigureAwait(false);

            if (created is null)
            {
                await RunOnUiAsync(() =>
                    SetError(_connection.LastError ?? "Service rejected the blocklist.")
                ).ConfigureAwait(false);
                return;
            }

            await RunOnUiAsync(() =>
            {
                Blocklists.Add(created);
                SelectedBlocklist = created;
                NewName = string.Empty;
                NewRulesText = string.Empty;
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsBusy = false).ConfigureAwait(false);
        }
    }

    private bool CanCreate() => !string.IsNullOrWhiteSpace(NewName);

    partial void OnNewNameChanged(string value) => CreateCommand.NotifyCanExecuteChanged();

    partial void OnSelectedBlocklistChanged(BlocklistDto? value) => OnPropertyChanged(nameof(HasSelection));

    [RelayCommand(CanExecute = nameof(CanDelete))]
    public async Task DeleteSelectedAsync(CancellationToken ct = default)
    {
        if (SelectedBlocklist is null) return;
        IsBusy = true;
        try
        {
            var ok = await _connection.DeleteBlocklistAsync(SelectedBlocklist.Id, ct).ConfigureAwait(false);
            if (!ok)
            {
                await RunOnUiAsync(() =>
                    SetError(_connection.LastError ?? "Service rejected the deletion.")
                ).ConfigureAwait(false);
                return;
            }
            await RunOnUiAsync(() =>
            {
                Blocklists.Remove(SelectedBlocklist);
                SelectedBlocklist = Blocklists.FirstOrDefault();
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsBusy = false).ConfigureAwait(false);
        }
    }

    private bool CanDelete() => SelectedBlocklist is not null;

    internal static IReadOnlyList<CreateRuleRequest> ParseRules(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<CreateRuleRequest>();
        var rules = new List<CreateRuleRequest>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith('#') || line.StartsWith('!')) continue;

            if (line.StartsWith("allow:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line["allow:".Length..].Trim();
                rules.Add(new CreateRuleRequest
                {
                    Type = value.Contains('*', StringComparison.Ordinal) ? "ExceptionWildcard"
                        : value.Contains('/', StringComparison.Ordinal) ? "ExceptionUrl"
                        : "ExceptionDomain",
                    Value = value,
                });
                continue;
            }
            if (line.StartsWith("category:", StringComparison.OrdinalIgnoreCase))
            {
                rules.Add(new CreateRuleRequest { Type = "Category", Value = line["category:".Length..].Trim() });
                continue;
            }
            if (line == "*.*" || line == "*")
            {
                rules.Add(new CreateRuleRequest { Type = "FullInternet", Value = "*" });
                continue;
            }
            if (line.Contains('*', StringComparison.Ordinal))
            {
                rules.Add(new CreateRuleRequest { Type = "Wildcard", Value = line });
                continue;
            }
            if (line.Contains('/', StringComparison.Ordinal))
            {
                rules.Add(new CreateRuleRequest { Type = "Url", Value = line });
                continue;
            }
            rules.Add(new CreateRuleRequest { Type = "Domain", Value = line });
        }
        return rules;
    }
}
