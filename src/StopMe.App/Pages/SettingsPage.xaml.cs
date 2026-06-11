using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StopMe.App.Controls;
using StopMe.App.Core.ViewModels;

namespace StopMe.App.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly MainWindow _window;
    private string _section = "general";

    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel, MainWindow window)
    {
        ViewModel = viewModel;
        _window = window;
        InitializeComponent();
        Loaded += (_, _) =>
        {
            BuildSubNav();
            RenderSection();
        };
    }

    private sealed record Section(string Id, string Label, string Icon);

    private static readonly Section[] Sections =
    {
        new("general",   "General",     "settings"),
        new("blocking",  "Blocking",    "shield"),
        new("privacy",   "Privacy",     "lock"),
        new("service",   "Service",     "monitor"),
        new("extension", "Extension",   "puzzle"),
        new("about",     "About",       "info"),
    };

    // ===== Sub-nav =====

    private void BuildSubNav()
    {
        SubNavStack.Children.Clear();
        foreach (var s in Sections)
        {
            var active = s.Id == _section;
            var btn = new Button
            {
                Style = (Style)Application.Current.Resources["StopMeNavItemButton"],
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = active
                    ? (Brush)Application.Current.Resources["StopMeBgPrimaryBrush"]
                    : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            };

            var row = new Grid { ColumnSpacing = 10, VerticalAlignment = VerticalAlignment.Center };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = new IconGlyph
            {
                IconName = s.Icon,
                Size = 16,
                ForegroundBrush = (Brush)Application.Current.Resources[active ? "StopMeTextPrimaryBrush" : "StopMeTextSecondaryBrush"],
            };
            Grid.SetColumn(icon, 0);
            row.Children.Add(icon);

            var label = new TextBlock
            {
                Text = s.Label,
                FontSize = 13.5,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources[active ? "StopMeTextPrimaryBrush" : "StopMeTextSecondaryBrush"],
            };
            Grid.SetColumn(label, 1);
            row.Children.Add(label);

            if (s.Id == "service"
                && ((MainWindow)_window).ViewModel.ConnectionState != Core.Services.ConnectionState.Connected
                && ((MainWindow)_window).ViewModel.ConnectionState != Core.Services.ConnectionState.Connecting)
            {
                var dot = new StatusDot { Tone = "error", Pulse = true };
                Grid.SetColumn(dot, 2);
                row.Children.Add(dot);
            }

            btn.Content = row;
            btn.Click += (_, _) => { _section = s.Id; BuildSubNav(); RenderSection(); };
            SubNavStack.Children.Add(btn);
        }
    }

    // ===== Section content =====

    private void RenderSection()
    {
        SectionContent.Children.Clear();
        SectionContent.Children.Add(_section switch
        {
            "general"   => BuildGeneralSection(),
            "blocking"  => BuildBlockingSection(),
            "privacy"   => BuildPrivacySection(),
            "service"   => BuildServiceSection(),
            "extension" => BuildExtensionSection(),
            "about"     => BuildAboutSection(),
            _           => BuildGeneralSection(),
        });
    }

    private FrameworkElement BuildGeneralSection()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "General", Style = (Style)Application.Current.Resources["StopMeSectionTitle"], Margin = new Thickness(0, 0, 0, 14) });
        sp.Children.Add(BuildRow("Start StopMe at boot", "Launch automatically when Windows starts.",
            new ToggleSwitch { IsOn = ViewModel.StartAtBoot, OnContent = "", OffContent = "" },
            (tg) => ViewModel.StartAtBoot = ((ToggleSwitch)tg).IsOn));
        sp.Children.Add(BuildRow("Start minimized", "Open in the system tray instead of a window.",
            new ToggleSwitch { IsOn = false, OnContent = "", OffContent = "" }, _ => { }));
        sp.Children.Add(BuildRow("Show tray icon", "Quick access to the active session from the taskbar.",
            new ToggleSwitch { IsOn = ViewModel.ShowTrayIcon, OnContent = "", OffContent = "" },
            (tg) => ViewModel.ShowTrayIcon = ((ToggleSwitch)tg).IsOn,
            isLast: true));
        sp.Children.Add(BuildSaveRow());
        return sp;
    }

    private FrameworkElement BuildBlockingSection()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Blocking", Style = (Style)Application.Current.Resources["StopMeSectionTitle"], Margin = new Thickness(0, 0, 0, 14) });

        var defaultDuration = new DurationSelector { Minutes = 60, Compact = true };
        sp.Children.Add(BuildRow("Default duration", "Pre-selected when you start a quick session.", defaultDuration, _ => { }));

        var modeSeg = BuildSegmented(new[] { "Normal", "Strict" }, "Normal");
        sp.Children.Add(BuildRow("Default mode", "Strict mode can't be stopped before the timer ends.", modeSeg, _ => { }));

        var combo = new ComboBox
        {
            ItemsSource = new[] { "Close the browser tab", "Block the whole browser", "Allow (not recommended)" },
            SelectedIndex = 0,
            Style = null,
            MinWidth = 240,
        };
        sp.Children.Add(BuildRow("Unsupported browsers",
            "What to do when a browser without the extension opens a blocked site.",
            combo, _ => { }, isLast: true));

        // Strict-mode callout
        var callout = new Border
        {
            Background = (Brush)Application.Current.Resources["StopMeBgSecondaryBrush"],
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(16, 13, 16, 13),
            Margin = new Thickness(0, 18, 0, 0),
        };
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lockIcon = new IconGlyph { IconName = "lock", Size = 16, VerticalAlignment = VerticalAlignment.Top, ForegroundBrush = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"] };
        Grid.SetColumn(lockIcon, 0);
        grid.Children.Add(lockIcon);

        var calloutText = new TextBlock
        {
            FontSize = 13,
            Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        };
        calloutText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
        {
            Text = "About strict mode. ",
            Foreground = (Brush)Application.Current.Resources["StopMeTextPrimaryBrush"],
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
        });
        calloutText.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run
        {
            Text = "Once a strict session starts, it cannot be stopped, edited, or bypassed by restarting StopMe or Windows. The timer always finishes.",
        });
        Grid.SetColumn(calloutText, 1);
        grid.Children.Add(calloutText);
        callout.Child = grid;
        sp.Children.Add(callout);
        return sp;
    }

    private FrameworkElement BuildPrivacySection()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Privacy", Style = (Style)Application.Current.Resources["StopMeSectionTitle"], Margin = new Thickness(0, 0, 0, 14) });

        var seg = BuildSegmented(new[] { "Readable", "Anonymized", "Disabled" }, "Readable");
        sp.Children.Add(BuildRow("Statistics mode",
            "Control how much detail StopMe keeps about blocked attempts.",
            seg, _ => { }));

        var exportBtn = new Button
        {
            Style = (Style)Application.Current.Resources["StopMeSecondaryButtonSmall"],
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new IconGlyph { IconName = "download", Size = 15, ForegroundBrush = (Brush)Application.Current.Resources["StopMeTextPrimaryBrush"] },
                    new TextBlock { Text = "Export", VerticalAlignment = VerticalAlignment.Center, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                },
            },
        };
        exportBtn.Click += (_, _) => _window.ShowToast("Data exported.");
        sp.Children.Add(BuildRow("Export data", "Download all your sessions and statistics as CSV.", exportBtn, _ => { }));

        var deleteBtn = new Button
        {
            Style = (Style)Application.Current.Resources["StopMeSecondaryButtonSmall"],
            Foreground = (Brush)Application.Current.Resources["StopMeErrorBrush"],
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new IconGlyph { IconName = "trash", Size = 15, ForegroundBrush = (Brush)Application.Current.Resources["StopMeErrorBrush"] },
                    new TextBlock { Text = "Delete", VerticalAlignment = VerticalAlignment.Center, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Brush)Application.Current.Resources["StopMeErrorBrush"] },
                },
            },
        };
        sp.Children.Add(BuildRow("Delete all data",
            "Erase sessions, blocklists and statistics. This can't be undone.",
            deleteBtn, _ => { }, isLast: true));
        return sp;
    }

    private FrameworkElement BuildServiceSection()
    {
        var sp = new StackPanel();
        var state = ((MainWindow)_window).ViewModel.ConnectionState;
        var serviceOk = state == Core.Services.ConnectionState.Connected;

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(new TextBlock { Text = "Service", Style = (Style)Application.Current.Resources["StopMeSectionTitle"] });
        var badge = new Border
        {
            Style = (Style)Application.Current.Resources[serviceOk ? "StopMeBadgeSuccess" : "StopMeBadgeError"],
            Child = new TextBlock
            {
                Text = serviceOk ? "All good" : "Issue detected",
                Style = (Style)Application.Current.Resources["StopMeBadgeText"],
                Foreground = (Brush)Application.Current.Resources[serviceOk ? "StopMeSuccessBrush" : "StopMeErrorBrush"],
            },
        };
        Grid.SetColumn(badge, 1);
        header.Children.Add(badge);
        header.Margin = new Thickness(0, 0, 0, 14);
        sp.Children.Add(header);

        sp.Children.Add(BuildRow("Connection",
            serviceOk ? "App is connected to the background service." : "The app can't reach the background service.",
            BuildStatusBadge(serviceOk, "Connected", "Disconnected"), _ => { }));

        sp.Children.Add(BuildRow("Background service",
            "stopme-svc.exe — handles blocking even when the app is closed.",
            BuildStatusBadge(serviceOk, "Running", "Stopped"), _ => { }));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var retry = new Button { Style = (Style)Application.Current.Resources["StopMeSecondaryButtonSmall"], Content = "Retry connection" };
        retry.Click += async (_, _) => await ((MainWindow)_window).ViewModel.ReconnectCommand.ExecuteAsync(null);
        actions.Children.Add(retry);
        actions.Children.Add(new Button { Style = (Style)Application.Current.Resources["StopMeSoftButton"], Content = "Run diagnostics" });
        actions.Children.Add(new Button { Style = (Style)Application.Current.Resources["StopMeGhostButtonSmall"], Content = "Open logs" });
        sp.Children.Add(BuildRow("Troubleshooting", null, actions, _ => { }, isLast: true));
        return sp;
    }

    private FrameworkElement BuildExtensionSection()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "Browser extension", Style = (Style)Application.Current.Resources["StopMeSectionTitle"], Margin = new Thickness(0, 0, 0, 8) });
        sp.Children.Add(new TextBlock
        {
            Text = "StopMe blocks websites through a lightweight browser extension.",
            Style = (Style)Application.Current.Resources["StopMeBodySecondary"],
            FontSize = 13.5,
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap,
        });

        sp.Children.Add(BuildRow("Chrome", "StopMe for Chrome v2.4.1",
            BuildStatusBadge(true, "Connected", "Not connected"), _ => { }));
        sp.Children.Add(BuildRow("Edge", "Not installed",
            new Button { Style = (Style)Application.Current.Resources["StopMeSecondaryButtonSmall"], Content = "Install" }, _ => { }));
        sp.Children.Add(BuildRow("Other browsers",
            "Firefox support is coming soon. Unsupported browsers follow your Blocking setting.",
            new Button { Style = (Style)Application.Current.Resources["StopMeGhostButtonSmall"], Content = "Instructions" }, _ => { }, isLast: true));
        return sp;
    }

    private FrameworkElement BuildAboutSection()
    {
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock { Text = "About", Style = (Style)Application.Current.Resources["StopMeSectionTitle"], Margin = new Thickness(0, 0, 0, 14) });
        sp.Children.Add(BuildRow("Version", null,
            new TextBlock
            {
                Text = "StopMe 2.1.0 (stable)",
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
                VerticalAlignment = VerticalAlignment.Center,
            }, _ => { }));
        sp.Children.Add(BuildRow("Data location", null,
            new TextBlock
            {
                Text = @"C:\Users\you\AppData\Roaming\StopMe",
                FontSize = 12.5,
                FontFamily = (FontFamily)Application.Current.Resources["StopMeMonoFontFamily"],
                Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
                VerticalAlignment = VerticalAlignment.Center,
            }, _ => { }));
        var checkBtn = new Button
        {
            Style = (Style)Application.Current.Resources["StopMeSecondaryButtonSmall"],
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new IconGlyph { IconName = "refresh", Size = 15, ForegroundBrush = (Brush)Application.Current.Resources["StopMeTextPrimaryBrush"] },
                    new TextBlock { Text = "Check now", VerticalAlignment = VerticalAlignment.Center, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                },
            },
        };
        sp.Children.Add(BuildRow("Check for updates", null, checkBtn, _ => { }, isLast: true));
        return sp;
    }

    // ===== Helpers =====

    private FrameworkElement BuildRow(string title, string? body, FrameworkElement control, Action<FrameworkElement> onChange, bool isLast = false)
    {
        var grid = new Grid
        {
            ColumnSpacing = 24,
            Padding = new Thickness(0, 16, 0, 16),
            BorderBrush = (Brush)Application.Current.Resources["StopMeBorderBrush"],
            BorderThickness = new Thickness(0, 0, 0, isLast ? 0 : 1),
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var stack = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14 });
        if (!string.IsNullOrEmpty(body))
            stack.Children.Add(new TextBlock
            {
                Text = body,
                Style = (Style)Application.Current.Resources["StopMeBodySecondary"],
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
            });
        Grid.SetColumn(stack, 0);
        grid.Children.Add(stack);

        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        if (control is ToggleSwitch t) t.Toggled += (_, _) => onChange(t);

        return grid;
    }

    private FrameworkElement BuildSegmented(string[] options, string current)
    {
        var border = new Border
        {
            Background = (Brush)Application.Current.Resources["StopMeBgSecondaryBrush"],
            CornerRadius = new CornerRadius(11),
            Padding = new Thickness(3),
        };
        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var opt in options)
        {
            var selected = opt == current;
            var btn = new Button
            {
                Background = (Brush)Application.Current.Resources[selected ? "StopMeBgPrimaryBrush" : "StopMeBgSecondaryBrush"],
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Foreground = (Brush)Application.Current.Resources[selected ? "StopMeTextPrimaryBrush" : "StopMeTextSecondaryBrush"],
                Padding = new Thickness(14, 7, 14, 7),
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                CornerRadius = new CornerRadius(9),
                Content = opt,
            };
            stack.Children.Add(btn);
        }
        border.Child = stack;
        return border;
    }

    private FrameworkElement BuildStatusBadge(bool ok, string okText, string offText)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new StatusDot { Tone = ok ? "success" : "error", Pulse = !ok });
        sp.Children.Add(new TextBlock
        {
            Text = ok ? okText : offText,
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = (Brush)Application.Current.Resources[ok ? "StopMeSuccessBrush" : "StopMeErrorBrush"],
            VerticalAlignment = VerticalAlignment.Center,
        });
        return sp;
    }

    private FrameworkElement BuildSaveRow()
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        var btn = new Button { Style = (Style)Application.Current.Resources["StopMePrimaryButtonSmall"], Content = "Save" };
        btn.Click += (_, _) => { ViewModel.SaveCommand.Execute(null); _window.ShowToast("Saved."); };
        sp.Children.Add(btn);
        return sp;
    }
}
