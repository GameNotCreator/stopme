using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StopMe.App.Controls;
using StopMe.App.Core.Models;
using StopMe.App.Core.Services;
using StopMe.App.Core.ViewModels;
using StopMe.App.Pages;
using WinRT.Interop;

namespace StopMe.App;

public sealed partial class MainWindow : Window
{
    private readonly DispatcherQueueTimer _tick;
    private DateTime? _sessionEndsAtUtc;
    private bool _sessionIsStrict;

    public MainViewModel ViewModel { get; }

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Title = "StopMe";

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Sensible default size so the white panel + sidebar both breathe.
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var id = Win32Interop.GetWindowIdFromWindow(hwnd);
            var aw = AppWindow.GetFromWindowId(id);
            aw.Resize(new Windows.Graphics.SizeInt32(1320, 860));
        }
        catch
        {
            // Non-fatal — sizing helper is best-effort.
        }

        _tick = DispatcherQueue.CreateTimer();
        _tick.Interval = TimeSpan.FromSeconds(1);
        _tick.Tick += (_, _) => UpdateSessionMiniCard();
        _tick.Start();

        BuildNav();
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateConnectionBanner();
        NavigateTo(ViewModel.SelectedItem?.Key);
    }

    // ===== Public surface used by pages =====

    public void GoTo(string key)
    {
        var item = ViewModel.Items.FirstOrDefault(i => i.Key == key)
                ?? ViewModel.FooterItems.FirstOrDefault(i => i.Key == key);
        if (item is null) return;
        ViewModel.SelectedItem = item;
        BuildNav();
        NavigateTo(key);
    }

    public void ShowToast(string message) => Toast.Show(message);

    public void UpdateSession(DateTime? endsAtUtc, bool isStrict)
    {
        _sessionEndsAtUtc = endsAtUtc;
        _sessionIsStrict = isStrict;
        UpdateSessionMiniCard();
    }

    // ===== Nav =====

    private void BuildNav()
    {
        NavStack.Children.Clear();
        foreach (var item in ViewModel.Items) NavStack.Children.Add(BuildNavButton(item));
        foreach (var item in ViewModel.FooterItems) NavStack.Children.Add(BuildNavButton(item));
    }

    private Button BuildNavButton(NavigationItem item)
    {
        var active = ViewModel.SelectedItem?.Key == item.Key;
        var btn = new Button
        {
            Style = (Style)Application.Current.Resources["StopMeNavItemButton"],
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };

        if (active)
        {
            btn.Background = (Brush)Application.Current.Resources["StopMeBgPrimaryBrush"];
        }

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new IconGlyph
        {
            IconName = item.IconGlyph,
            Size = 18,
            ForegroundBrush = (Brush)Application.Current.Resources[active ? "StopMeAccentBrush" : "StopMeTextSecondaryBrush"],
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var label = new TextBlock
        {
            Text = item.Label,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(11, 0, 0, 0),
            Foreground = (Brush)Application.Current.Resources[active ? "StopMeTextPrimaryBrush" : "StopMeTextSecondaryBrush"],
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        // Dashboard badge: live countdown pill when a session is running
        if (item.Key == "dashboard" && _sessionEndsAtUtc is { } endsUtc)
        {
            var pill = BuildCountdownPill(endsUtc, _sessionIsStrict);
            Grid.SetColumn(pill, 2);
            grid.Children.Add(pill);
        }

        // Settings badge: red dot when service has a problem
        if (item.Key == "settings"
            && ViewModel.ConnectionState != ConnectionState.Connected
            && ViewModel.ConnectionState != ConnectionState.Connecting)
        {
            var dot = new StatusDot { Tone = "error", Pulse = true, Margin = new Thickness(8, 0, 0, 0) };
            Grid.SetColumn(dot, 2);
            grid.Children.Add(dot);
        }

        btn.Content = grid;
        btn.Click += (_, _) =>
        {
            ViewModel.SelectedItem = item;
            BuildNav();
            NavigateTo(item.Key);
        };
        return btn;
    }

    private Border BuildCountdownPill(DateTime endsAtUtc, bool isStrict)
    {
        var leftSec = (int)Math.Max(0, (endsAtUtc - DateTime.UtcNow).TotalSeconds);
        var brushKey = isStrict ? "StopMeStrictBrush" : "StopMeAccentBrush";
        return new Border
        {
            Background = (Brush)Application.Current.Resources[brushKey],
            CornerRadius = new CornerRadius(99),
            Padding = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            Child = new TextBlock
            {
                Text = FormatCountdown(leftSec),
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
            },
        };
    }

    private void NavigateTo(string? key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var sp = App.Services;
        switch (key)
        {
            case "dashboard":
                ContentFrame.Content = new DashboardPage(sp.GetRequiredService<DashboardViewModel>(), this);
                break;
            case "apps":
            case "block-apps":
                ContentFrame.Content = new BlockAppsPage(sp.GetRequiredService<BlockAppsViewModel>(), this);
                break;
            case "websites":
                ContentFrame.Content = new BlockWebsitesPage(sp.GetRequiredService<BlockWebsitesViewModel>(), this);
                break;
            case "sessions":
                ContentFrame.Content = new SessionsPage(sp.GetRequiredService<SessionsViewModel>(), this);
                break;
            case "stats":
                ContentFrame.Content = new StatisticsPage(sp.GetRequiredService<StatisticsViewModel>());
                break;
            case "settings":
                ContentFrame.Content = new SettingsPage(sp.GetRequiredService<SettingsViewModel>(), this);
                break;
        }
    }

    // ===== Session mini-card =====

    private void UpdateSessionMiniCard()
    {
        if (_sessionEndsAtUtc is null)
        {
            SessionMiniCard.Visibility = Visibility.Collapsed;
            BuildNav();
            return;
        }

        var leftSec = (int)Math.Max(0, (_sessionEndsAtUtc.Value - DateTime.UtcNow).TotalSeconds);
        if (leftSec == 0)
        {
            _sessionEndsAtUtc = null;
            SessionMiniCard.Visibility = Visibility.Collapsed;
            BuildNav();
            return;
        }

        SessionMiniCard.Visibility = Visibility.Visible;
        SessionMiniCard.Background = _sessionIsStrict
            ? (Brush)Application.Current.Resources["StopMeTextPrimaryBrush"]
            : (Brush)Application.Current.Resources["StopMeAccentGradientBrush"];
        MiniSessionLabel.Text = _sessionIsStrict ? "STRICT SESSION" : "BLOCKING ACTIVE";
        MiniSessionCountdown.Text = FormatCountdown(leftSec);
        MiniSessionEndsAt.Text = $"until {_sessionEndsAtUtc.Value.ToLocalTime():h:mm tt}";
        BuildNav();
    }

    // ===== Connection banner =====

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(MainViewModel.ConnectionState) or nameof(MainViewModel.ConnectionLabel)))
            return;

        if (DispatcherQueue.HasThreadAccess) UpdateConnectionBanner();
        else DispatcherQueue.TryEnqueue(UpdateConnectionBanner);
    }

    private void UpdateConnectionBanner()
    {
        var state = ViewModel.ConnectionState;
        switch (state)
        {
            case ConnectionState.Connected:
                ConnectionBanner.Visibility = Visibility.Collapsed;
                ServiceStatusDot.Tone = "success";
                ServiceStatusDot.Pulse = false;
                ServiceStatusLabel.Text = "Service connected";
                ServiceStatusLabel.Foreground = (Brush)Application.Current.Resources["StopMeTextTertiaryBrush"];
                break;

            case ConnectionState.Connecting:
                ConnectionBanner.Visibility = Visibility.Visible;
                ConnectionBanner.Background = (Brush)Application.Current.Resources["StopMeBgPrimaryBrush"];
                ConnectionBanner.BorderBrush = (Brush)Application.Current.Resources["StopMeBorderBrush"];
                ConnectionRing.IsActive = true;
                ConnectionTitle.Text = "Connecting to StopMe service…";
                ConnectionBody.Text = "This usually takes a couple of seconds.";
                ReconnectButton.Visibility = Visibility.Collapsed;
                ServiceStatusDot.Tone = "neutral";
                ServiceStatusLabel.Text = "Connecting…";
                ServiceStatusLabel.Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"];
                break;

            default:
                ConnectionBanner.Visibility = Visibility.Visible;
                ConnectionBanner.Background = (Brush)Application.Current.Resources["StopMeErrorSoftBrush"];
                ConnectionBanner.BorderBrush = (Brush)Application.Current.Resources["StopMeErrorBrush"];
                ConnectionRing.IsActive = false;
                ConnectionTitle.Text = "StopMe service is not running.";
                ConnectionBody.Text = ViewModel.ConnectionLabel
                    ?? "Blocking won't work until the service is back.";
                ReconnectButton.Visibility = Visibility.Visible;
                ServiceStatusDot.Tone = "error";
                ServiceStatusDot.Pulse = true;
                ServiceStatusLabel.Text = "Service issue";
                ServiceStatusLabel.Foreground = (Brush)Application.Current.Resources["StopMeErrorBrush"];
                BuildNav();
                break;
        }
    }

    // ===== Title bar caption buttons =====

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var presenter = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd)).Presenter;
        if (presenter is OverlappedPresenter op) op.Minimize();
    }

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var presenter = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd)).Presenter;
        if (presenter is OverlappedPresenter op)
        {
            if (op.State == OverlappedPresenterState.Maximized) op.Restore();
            else op.Maximize();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private async void OnReconnectClick(object sender, RoutedEventArgs e) =>
        await ViewModel.ReconnectCommand.ExecuteAsync(null);

    private void OnServiceChipClick(object sender, RoutedEventArgs e)
    {
        // Open the Settings page on the Service section. Diagnostics modal is
        // exposed from the Settings page itself.
        GoTo("settings");
    }

    private static string FormatCountdown(int totalSec)
    {
        var h = totalSec / 3600;
        var m = (totalSec % 3600) / 60;
        var s = totalSec % 60;
        return h > 0
            ? $"{h}:{m:00}:{s:00}"
            : $"{m}:{s:00}";
    }
}
