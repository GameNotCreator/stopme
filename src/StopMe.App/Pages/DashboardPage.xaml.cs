using System;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using StopMe.App.Controls;
using StopMe.App.Core.Services;
using StopMe.App.Core.ViewModels;
using StopMe.LocalApi.Dtos;

namespace StopMe.App.Pages;

public sealed partial class DashboardPage : Page
{
    private readonly MainWindow _window;
    private readonly DispatcherQueueTimer _tick;
    private ActiveSessionDto? _activeSession;

    public DashboardViewModel ViewModel { get; }

    public DashboardPage(DashboardViewModel viewModel, MainWindow window)
    {
        ViewModel = viewModel;
        _window = window;
        InitializeComponent();

        StrictToggle.Toggled += (_, _) => RenderEmpty();
        Duration.MinutesChanged += (_, _) => RenderEmpty();

        _tick = DispatcherQueue.CreateTimer();
        _tick.Interval = TimeSpan.FromSeconds(1);
        _tick.Tick += (_, _) => UpdateCountdown();

        Loaded += async (_, _) =>
        {
            await ViewModel.RefreshAsync();
            Render();
            _tick.Start();
        };
        Unloaded += (_, _) => _tick.Stop();
    }

    // ===== Top-level render dispatch =====

    private void Render()
    {
        _activeSession = ViewModel.ActiveSessions.FirstOrDefault();
        if (_activeSession is null)
        {
            EmptyHero.Visibility = Visibility.Visible;
            ActiveHero.Visibility = Visibility.Collapsed;
            _window.UpdateSession(null, false);
            RenderEmpty();
        }
        else
        {
            EmptyHero.Visibility = Visibility.Collapsed;
            ActiveHero.Visibility = Visibility.Visible;
            _window.UpdateSession(_activeSession.EndAtUtc, IsStrict(_activeSession));
            RenderActive();
        }

        RenderInlineServiceStatus();
    }

    // ===== Empty state (hero) =====

    private void RenderEmpty()
    {
        var totalApps = 0;            // TODO wire to BlockAppsViewModel selection persistence
        var totalSites = 0;           // TODO wire to BlockWebsitesViewModel selection persistence
        var durationMins = Duration.Minutes;
        var strict = StrictToggle.IsStrict;

        BuildSummaryChips(totalApps, totalSites, durationMins, strict);

        NothingHint.Visibility = (totalApps + totalSites) == 0 ? Visibility.Visible : Visibility.Collapsed;
        StartButton.IsEnabled = (totalApps + totalSites) > 0;
    }

    private void BuildSummaryChips(int apps, int sites, int durationMins, bool strict)
    {
        var items = new[]
        {
            new ChipItem("apps",       Plural(apps, "app", "apps"),     Empty:apps == 0,  Page: "apps"),
            new ChipItem("globe",      Plural(sites, "website", "websites"), Empty:sites == 0, Page: "websites"),
            new ChipItem("clock",      FormatDuration(durationMins),    Empty:false,      Page: null),
            new ChipItem(strict ? "lock" : "unlock", strict ? "Strict" : "Normal", Empty:false, Page: null),
        };
        SummaryChips.Items.Clear();
        foreach (var it in items) SummaryChips.Items.Add(BuildChip(it));
    }

    private Button BuildChip(ChipItem c)
    {
        var btn = new Button
        {
            Background = (Brush)Application.Current.Resources["StopMeBgPrimaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["StopMeBorderBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(99),
            Padding = new Thickness(14, 7, 14, 7),
            MinHeight = 32,
        };
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new IconGlyph
        {
            IconName = c.IconName,
            Size = 14,
            ForegroundBrush = (Brush)Application.Current.Resources[c.Empty ? "StopMeTextTertiaryBrush" : "StopMeAccentBrush"],
        });
        stack.Children.Add(new TextBlock
        {
            Text = c.Label,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources[c.Empty ? "StopMeTextTertiaryBrush" : "StopMeTextPrimaryBrush"],
        });
        if (c.Page is not null)
        {
            stack.Children.Add(new IconGlyph
            {
                IconName = "chevronRight",
                Size = 12,
                ForegroundBrush = (Brush)Application.Current.Resources["StopMeTextTertiaryBrush"],
            });
            btn.Click += (_, _) => _window.GoTo(c.Page);
        }
        btn.Content = stack;
        return btn;
    }

    // ===== Active state (ring) =====

    private void RenderActive()
    {
        if (_activeSession is null) return;

        var strict = IsStrict(_activeSession);
        ActiveSessionName.Text = _activeSession.Name;

        if (strict)
        {
            ActiveCardBorder.BorderBrush = (Brush)Application.Current.Resources["StopMeStrictBrush"];
            ActiveCardBorder.BorderThickness = new Thickness(1.5);
            ActiveModeBadge.Style = (Style)Application.Current.Resources["StopMeBadgeStrict"];
            ActiveModeBadgeText.Text = "STRICT";
            ActiveModeBadgeText.Foreground = new SolidColorBrush(Colors.White);
            ActiveRing.RingBrush = (Brush)Application.Current.Resources["StopMeStrictBrush"];
            ActiveRing.TrackBrush = (Brush)Application.Current.Resources["StopMeBgTertiaryBrush"];
            ActiveStatusDot.Tone = "accent";
            StopButton.Visibility = Visibility.Collapsed;
            LockedButton.Visibility = Visibility.Visible;
            LockedButtonText.Text = $"Locked until {_activeSession.EndAtUtc.ToLocalTime():h:mm tt}";
        }
        else
        {
            ActiveCardBorder.BorderBrush = (Brush)Application.Current.Resources["StopMeAccentSoft2Brush"];
            ActiveCardBorder.BorderThickness = new Thickness(1);
            ActiveModeBadge.Style = (Style)Application.Current.Resources["StopMeBadge"];
            ActiveModeBadgeText.Text = "NORMAL";
            ActiveModeBadgeText.Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"];
            ActiveRing.RingBrush = (Brush)Application.Current.Resources["StopMeAccentBrush"];
            ActiveRing.TrackBrush = (Brush)Application.Current.Resources["StopMeAccentSoft2Brush"];
            ActiveStatusDot.Tone = "success";
            StopButton.Visibility = Visibility.Visible;
            LockedButton.Visibility = Visibility.Collapsed;
        }

        ProtectingCaption.Text =
            $"StopMe is protecting your focus until {_activeSession.EndAtUtc.ToLocalTime():h:mm tt}";

        // Blocked chips placeholder — backend will eventually return the apps/sites tied to the
        // session; for now show a count summary chip so the layout reads correctly.
        BlockedChips.Items.Clear();
        BlockedChips.Items.Add(new Border
        {
            Background = (Brush)Application.Current.Resources["StopMeBgSecondaryBrush"],
            CornerRadius = new CornerRadius(99),
            Padding = new Thickness(10, 4, 10, 4),
            Child = new TextBlock
            {
                Text = $"{_activeSession.BlocklistIds.Count} blocklist(s) active",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
            },
        });

        UpdateCountdown();
    }

    private void UpdateCountdown()
    {
        if (_activeSession is null) return;
        var totalSec = Math.Max(1, (int)(_activeSession.EndAtUtc - _activeSession.StartAtUtc).TotalSeconds);
        var leftSec = Math.Max(0, (int)(_activeSession.EndAtUtc - DateTime.UtcNow).TotalSeconds);
        var progress = Math.Clamp(1.0 - (double)leftSec / totalSec, 0.0, 1.0);
        ActiveRing.Progress = progress;
        CountdownText.Text = FormatCountdown(leftSec);

        if (leftSec == 0)
        {
            _window.ShowToast("Session complete — nice work!");
            _activeSession = null;
            Render();
        }
    }

    // ===== Service status inline button =====

    private void RenderInlineServiceStatus()
    {
        var state = ((MainWindow)_window).ViewModel.ConnectionState;
        var label = state switch
        {
            ConnectionState.Connected  => "Service connected",
            ConnectionState.Connecting => "Connecting to StopMe service…",
            _                          => "Service issue — click for details",
        };
        var tone = state switch
        {
            ConnectionState.Connected  => "success",
            ConnectionState.Connecting => "neutral",
            _                          => "error",
        };
        InlineStatusDot.Tone = tone;
        InlineStatusDot.Pulse = state != ConnectionState.Connected;
        InlineStatusLabel.Text = label;
        ActiveInlineStatusDot.Tone = tone;
        ActiveInlineStatusDot.Pulse = state != ConnectionState.Connected;
        ActiveInlineStatusLabel.Text = label;
    }

    // ===== User actions =====

    private async void OnStartClick(object sender, RoutedEventArgs e)
    {
        if (StartButton.IsEnabled == false) return;
        if (StrictToggle.IsStrict)
        {
            await StrictConfirmAsync();
            return;
        }
        await ViewModel.RefreshAsync();
        Render();
    }

    private async Task StrictConfirmAsync()
    {
        var dialog = new ContentDialog
        {
            Title = "Start a strict session?",
            Content = new TextBlock
            {
                Text = "Strict mode can't be stopped before the timer ends. " +
                       "Not by closing StopMe, not by restarting Windows.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
            },
            PrimaryButtonText = "I'm sure — lock me in",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            _window.ShowToast("Strict session would start here (wiring TODO).");
        }
    }

    private void OnCreateCustomClick(object sender, RoutedEventArgs e)
    {
        _window.GoTo("sessions");
    }

    private async void OnStopClick(object sender, RoutedEventArgs e)
    {
        if (_activeSession is null) return;
        var dialog = new ContentDialog
        {
            Title = "Stop this session?",
            Content = new TextBlock
            {
                Text = $"{FormatCountdown((int)Math.Max(0, (_activeSession.EndAtUtc - DateTime.UtcNow).TotalSeconds))} still remaining. "
                       + "Blocked apps and sites will be available again immediately.",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "Keep focusing",
            SecondaryButtonText = "Stop session",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var r = await dialog.ShowAsync();
        if (r == ContentDialogResult.Secondary)
        {
            // The service-side stop is reachable via ActiveSessionViewModel — for now we
            // simply refresh; full stop wiring is the Sessions page command.
            await ViewModel.RefreshAsync();
            Render();
        }
    }

    private void OnLockedClick(object sender, RoutedEventArgs e)
    {
        LockedTooltip.Visibility = Visibility.Visible;
        Shake(LockedButton);
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(3200);
            LockedTooltip.Visibility = Visibility.Collapsed;
        });
    }

    private static void Shake(FrameworkElement target)
    {
        target.RenderTransform = new TranslateTransform();
        var anim = new DoubleAnimationUsingKeyFrames();
        anim.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(0),   Value = 0 });
        anim.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(90),  Value = -6 });
        anim.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(180), Value =  6 });
        anim.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(270), Value = -4 });
        anim.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(360), Value =  4 });
        anim.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = TimeSpan.FromMilliseconds(450), Value =  0 });
        Storyboard.SetTarget(anim, target.RenderTransform);
        Storyboard.SetTargetProperty(anim, "X");
        var sb = new Storyboard();
        sb.Children.Add(anim);
        sb.Begin();
    }

    // ===== Helpers =====

    private static bool IsStrict(ActiveSessionDto s) =>
        string.Equals(s.LockPolicy, "TimeLocked", StringComparison.OrdinalIgnoreCase)
        || string.Equals(s.Mode, "Strict", StringComparison.OrdinalIgnoreCase);

    private static string Plural(int n, string singular, string plural) =>
        $"{n} {(n == 1 ? singular : plural)}";

    private static string FormatDuration(int mins)
    {
        if (mins < 60) return $"{mins} min";
        var h = mins / 60;
        var m = mins % 60;
        return m == 0 ? $"{h} h" : $"{h} h {m} min";
    }

    private static string FormatCountdown(int totalSec)
    {
        var h = totalSec / 3600;
        var m = (totalSec % 3600) / 60;
        var s = totalSec % 60;
        return h > 0 ? $"{h}:{m:00}:{s:00}" : $"{m}:{s:00}";
    }

    private sealed record ChipItem(string IconName, string Label, bool Empty, string? Page);
}
