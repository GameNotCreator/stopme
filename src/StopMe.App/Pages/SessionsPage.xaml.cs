using System;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StopMe.App.Controls;
using StopMe.App.Core.ViewModels;
using StopMe.LocalApi.Dtos;

namespace StopMe.App.Pages;

public sealed partial class SessionsPage : Page
{
    private readonly MainWindow _window;

    public SessionsViewModel ViewModel { get; }

    public SessionsPage(SessionsViewModel viewModel, MainWindow window)
    {
        ViewModel = viewModel;
        _window = window;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await ViewModel.RefreshAsync();
            RenderActive();
            RenderScheduled();
            RenderRecurring();
            RenderPast();
        };
    }

    private void RenderActive()
    {
        ActiveContainer.Children.Clear();
        if (ViewModel.Active.FirstOrDefault() is { } current)
        {
            ActiveContainer.Children.Add(WrapInCard(BuildSessionRow(
                icon: "play",
                name: current.Name,
                badges: BuildBadge(IsStrict(current) ? "STRICT" : "RUNNING", IsStrict(current) ? "strict" : "success"),
                meta: $"{FormatRemaining(current)} remaining · {current.BlocklistIds.Count} blocklist(s)",
                actions: BuildActionRow(IsStrict(current)
                    ? (FrameworkElement)BuildLockedButton()
                    : BuildStopButton(current)))));
        }
        else
        {
            var dashed = new Border
            {
                Background = (Brush)Application.Current.Resources["StopMeBgSecondaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources["StopMeBorderStrongBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
            };
            var empty = new EmptyState
            {
                IconName = "moon",
                Title = "No active session",
                Body = "Start one from the Dashboard, or create a custom session here.",
                Action = MakeSoftBtn("plus", "Create session", OnNewSessionClick),
            };
            dashed.Child = empty;
            ActiveContainer.Children.Add(dashed);
        }
    }

    private void RenderScheduled()
    {
        ScheduledList.Items.Clear();
        var n = ViewModel.Scheduled.Count;
        for (var i = 0; i < n; i++)
        {
            var s = ViewModel.Scheduled[i];
            var row = BuildSessionRow(
                icon: "sessions",
                name: s.Name,
                badges: s.Strict ? BuildBadge("STRICT", "strict") : null,
                meta: $"{s.When} · {FormatDuration(s.DurationMinutes)} · {s.Apps} apps · {s.Sites} websites",
                actions: BuildActionRow(
                    MakeGhostBtn("edit", "Edit", null),
                    MakeGhostDangerBtn("trash", null)));
            ScheduledList.Items.Add(WrapRowWithDivider(row, i == n - 1));
        }
    }

    private void RenderRecurring()
    {
        RecurringList.Items.Clear();
        var n = ViewModel.Recurring.Count;
        for (var i = 0; i < n; i++)
        {
            var s = ViewModel.Recurring[i];
            var toggle = new ToggleSwitch
            {
                IsOn = s.Enabled,
                OnContent = "",
                OffContent = "",
                MinWidth = 42,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var badgesStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            if (s.Strict) badgesStack.Children.Add(BuildBadge("STRICT", "strict"));
            if (!s.Enabled) badgesStack.Children.Add(BuildBadge("PAUSED", "neutral"));

            var row = BuildSessionRow(
                icon: "repeat",
                name: s.Name,
                badges: badgesStack,
                meta: $"{s.Days} · {s.Time} · {s.Apps} apps · {s.Sites} websites",
                actions: BuildActionRow(toggle),
                dim: !s.Enabled);
            RecurringList.Items.Add(WrapRowWithDivider(row, i == n - 1));
        }
    }

    private void RenderPast()
    {
        PastList.Items.Clear();
        var n = ViewModel.Past.Count;
        for (var i = 0; i < n; i++)
        {
            var s = ViewModel.Past[i];
            var badge = s.Completed ? BuildBadge("COMPLETED", "success") : BuildBadge("STOPPED EARLY", "warning");
            var metaText = s.Completed
                ? $"{s.Date} · {FormatDuration(s.DurationMinutes)} · {s.Apps} apps · {s.Sites} websites"
                : $"{s.Date} · {FormatDuration(s.StoppedAtMins)} of {FormatDuration(s.DurationMinutes)} · {s.Apps} apps · {s.Sites} websites";
            var row = BuildSessionRow(
                icon: "history",
                name: s.Name,
                badges: badge,
                meta: metaText,
                actions: BuildActionRow(MakeGhostBtn("repeat", "Repeat", null)),
                dim: true);
            PastList.Items.Add(WrapRowWithDivider(row, i == n - 1));
        }
    }

    // ===== Row builders =====

    private FrameworkElement BuildSessionRow(string icon, string name, FrameworkElement? badges, string meta, FrameworkElement actions, bool dim = false)
    {
        var grid = new Grid
        {
            Padding = new Thickness(20, 14, 20, 14),
            ColumnSpacing = 14,
            Opacity = dim ? 0.62 : 1.0,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Icon tile
        var tile = new Border
        {
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(12),
            Background = (Brush)Application.Current.Resources["StopMeBgSecondaryBrush"],
            Child = new IconGlyph
            {
                IconName = icon,
                Size = 18,
                ForegroundBrush = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(tile, 0);
        grid.Children.Add(tile);

        // Centre column: name + badges + meta
        var middle = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        nameRow.Children.Add(new TextBlock
        {
            Text = name,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (badges is not null)
        {
            badges.VerticalAlignment = VerticalAlignment.Center;
            nameRow.Children.Add(badges);
        }
        middle.Children.Add(nameRow);
        middle.Children.Add(new TextBlock
        {
            Text = meta,
            FontSize = 12.5,
            Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        });
        Grid.SetColumn(middle, 1);
        grid.Children.Add(middle);

        Grid.SetColumn(actions, 2);
        grid.Children.Add(actions);
        return grid;
    }

    private FrameworkElement WrapInCard(FrameworkElement row)
    {
        return new Border
        {
            Background = (Brush)Application.Current.Resources["StopMeBgPrimaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["StopMeBorderBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Child = row,
        };
    }

    private FrameworkElement WrapRowWithDivider(FrameworkElement row, bool isLast)
    {
        return new Border
        {
            BorderBrush = (Brush)Application.Current.Resources["StopMeBorderBrush"],
            BorderThickness = new Thickness(0, 0, 0, isLast ? 0 : 1),
            Child = row,
        };
    }

    private static Border BuildBadge(string text, string tone)
    {
        var styleKey = tone switch
        {
            "success" => "StopMeBadgeSuccess",
            "warning" => "StopMeBadgeWarning",
            "error"   => "StopMeBadgeError",
            "strict"  => "StopMeBadgeStrict",
            "accent"  => "StopMeBadgeAccent",
            _         => "StopMeBadge",
        };
        var fgKey = tone switch
        {
            "success" => "StopMeSuccessBrush",
            "warning" => "StopMeWarningBrush",
            "error"   => "StopMeErrorBrush",
            "strict"  => "StopMeBgPrimaryBrush",
            "accent"  => "StopMeAccentTextBrush",
            _         => "StopMeTextSecondaryBrush",
        };
        return new Border
        {
            Style = (Style)Application.Current.Resources[styleKey],
            Child = new TextBlock
            {
                Text = text,
                Style = (Style)Application.Current.Resources["StopMeBadgeText"],
                Foreground = (Brush)Application.Current.Resources[fgKey],
            },
        };
    }

    private static StackPanel BuildActionRow(params FrameworkElement[] children)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        foreach (var c in children) sp.Children.Add(c);
        return sp;
    }

    private Button BuildStopButton(ActiveSessionDto session)
    {
        var btn = new Button
        {
            Style = (Style)Application.Current.Resources["StopMeGhostButtonSmall"],
            Foreground = (Brush)Application.Current.Resources["StopMeErrorBrush"],
            Content = BuildIconLabel("stop", "Stop", (Brush)Application.Current.Resources["StopMeErrorBrush"], 13),
        };
        btn.Click += async (_, _) =>
        {
            var dialog = new ContentDialog
            {
                Title = "Stop this session?",
                Content = new TextBlock
                {
                    Text = $"{FormatRemaining(session)} still remaining. Blocked apps and sites will be available again immediately.",
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
                await ViewModel.StopActiveCommand.ExecuteAsync(null);
                RenderActive();
                _window.ShowToast("Session stopped");
            }
        };
        return btn;
    }

    private Button BuildLockedButton()
    {
        var btn = new Button
        {
            Style = (Style)Application.Current.Resources["StopMeGhostButtonSmall"],
            Content = BuildIconLabel("lock", "Locked", (Brush)Application.Current.Resources["StopMeTextTertiaryBrush"], 13),
            IsEnabled = false,
        };
        return btn;
    }

    private Button MakeGhostBtn(string icon, string label, RoutedEventHandler? onClick)
    {
        var btn = new Button
        {
            Style = (Style)Application.Current.Resources["StopMeGhostButtonSmall"],
            Content = string.IsNullOrEmpty(label)
                ? (object)new IconGlyph { IconName = icon, Size = 15, ForegroundBrush = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"] }
                : BuildIconLabel(icon, label, (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"], 13),
        };
        if (onClick is not null) btn.Click += onClick;
        return btn;
    }

    private Button MakeGhostDangerBtn(string icon, RoutedEventHandler? onClick)
    {
        var btn = new Button
        {
            Style = (Style)Application.Current.Resources["StopMeGhostButtonSmall"],
            Foreground = (Brush)Application.Current.Resources["StopMeErrorBrush"],
            Content = new IconGlyph { IconName = icon, Size = 15, ForegroundBrush = (Brush)Application.Current.Resources["StopMeErrorBrush"] },
        };
        if (onClick is not null) btn.Click += onClick;
        return btn;
    }

    private Button MakeSoftBtn(string icon, string label, RoutedEventHandler? onClick)
    {
        var btn = new Button
        {
            Style = (Style)Application.Current.Resources["StopMeSoftButton"],
            Content = BuildIconLabel(icon, label, (Brush)Application.Current.Resources["StopMeAccentTextBrush"], 13.5),
        };
        if (onClick is not null) btn.Click += onClick;
        return btn;
    }

    private FrameworkElement BuildIconLabel(string icon, string label, Brush fg, double fontSize)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new IconGlyph { IconName = icon, Size = 15, ForegroundBrush = fg });
        sp.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = fontSize,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = fg,
        });
        return sp;
    }

    // ===== Helpers =====

    private static bool IsStrict(ActiveSessionDto s) =>
        string.Equals(s.LockPolicy, "TimeLocked", StringComparison.OrdinalIgnoreCase)
        || string.Equals(s.Mode, "Strict", StringComparison.OrdinalIgnoreCase);

    private static string FormatRemaining(ActiveSessionDto s)
    {
        var sec = Math.Max(0, (int)(s.EndAtUtc - DateTime.UtcNow).TotalSeconds);
        var h = sec / 3600;
        var m = (sec % 3600) / 60;
        var ss = sec % 60;
        return h > 0 ? $"{h}:{m:00}:{ss:00}" : $"{m}:{ss:00}";
    }

    private static string FormatDuration(int mins)
    {
        if (mins < 60) return $"{mins} min";
        var h = mins / 60;
        var m = mins % 60;
        return m == 0 ? $"{h} h" : $"{h} h {m} min";
    }

    private async void OnNewSessionClick(object sender, RoutedEventArgs e)
    {
        // Minimal create-session dialog. Full spec calls for a 560px modal with
        // name + apps/websites picker + duration + strict + start/schedule;
        // the prototype reaches those pages via navigation, so we follow suit.
        var dialog = new ContentDialog
        {
            Title = "Create a session",
            Content = new TextBlock
            {
                Text = "Pick the apps and websites to block from the Block Apps / Block Websites pages, then return here to schedule.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
            },
            PrimaryButtonText = "Block Apps",
            SecondaryButtonText = "Block Websites",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var r = await dialog.ShowAsync();
        if (r == ContentDialogResult.Primary) _window.GoTo("apps");
        else if (r == ContentDialogResult.Secondary) _window.GoTo("websites");
    }
}
