using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using StopMe.App.Controls;
using StopMe.App.Core.ViewModels;

namespace StopMe.App.Pages;

public sealed partial class BlockAppsPage : Page
{
    private readonly MainWindow _window;
    private string _query = string.Empty;
    private bool _showInstalled;

    public BlockAppsViewModel ViewModel { get; }

    public BlockAppsPage(BlockAppsViewModel viewModel, MainWindow window)
    {
        ViewModel = viewModel;
        _window = window;
        InitializeComponent();

        StickyDuration.MinutesChanged += (_, m) => ViewModel.DurationMinutes = m;
        StickyStrict.Toggled += (_, on) => ViewModel.SelectedMode = on ? "Strict" : "Soft";

        ViewModel.RunningApps.CollectionChanged += (_, _) => RenderLists();
        ViewModel.InstalledApps.CollectionChanged += (_, _) => RenderLists();
        ViewModel.ManualEntries.CollectionChanged += (_, _) => RenderLists();

        Loaded += async (_, _) =>
        {
            StickyDuration.Minutes = ViewModel.DurationMinutes;
            StickyStrict.IsStrict = string.Equals(ViewModel.SelectedMode, "Strict", StringComparison.OrdinalIgnoreCase);
            await ViewModel.RefreshAsync();
            UpdateTabStyles();
            RenderLists();
        };
    }

    private void OnRunningTabClick(object sender, RoutedEventArgs e)
    {
        _showInstalled = false;
        SearchBox.PlaceholderText = "Search running apps";
        UpdateTabStyles();
        RenderLists();
    }

    private void OnInstalledTabClick(object sender, RoutedEventArgs e)
    {
        _showInstalled = true;
        SearchBox.PlaceholderText = "Search installed apps";
        UpdateTabStyles();
        RenderLists();
    }

    private void UpdateTabStyles()
    {
        if (_showInstalled)
        {
            InstalledTabBtn.Style = (Style)Application.Current.Resources["StopMeSecondaryButtonSmall"];
            RunningTabBtn.Style = (Style)Application.Current.Resources["StopMeGhostButtonSmall"];
        }
        else
        {
            RunningTabBtn.Style = (Style)Application.Current.Resources["StopMeSecondaryButtonSmall"];
            InstalledTabBtn.Style = (Style)Application.Current.Resources["StopMeGhostButtonSmall"];
        }
        InstalledTabLabel.Text = ViewModel.InstalledApps.Count > 0
            ? $"Installed ({ViewModel.InstalledApps.Count})"
            : "Installed";
        RunningTabLabel.Text = ViewModel.RunningApps.Count > 0
            ? $"Running ({ViewModel.RunningApps.Count})"
            : "Running";
    }

    // ===== Row renderers =====

    private void RenderLists()
    {
        RunningAppsList.Items.Clear();
        int n;
        if (_showInstalled)
        {
            var filtered = ViewModel.InstalledApps
                .Where(a => string.IsNullOrEmpty(_query)
                            || a.DisplayName.Contains(_query, StringComparison.OrdinalIgnoreCase)
                            || (a.ProcessName ?? string.Empty).Contains(_query, StringComparison.OrdinalIgnoreCase)
                            || (a.Publisher ?? string.Empty).Contains(_query, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            n = filtered.Length;
            if (ViewModel.IsBusy)
            {
                RefreshingPanel.Visibility = Visibility.Visible;
                RunningEmpty.Visibility = Visibility.Collapsed;
            }
            else
            {
                RefreshingPanel.Visibility = Visibility.Collapsed;
                RunningEmpty.Visibility = n == 0 ? Visibility.Visible : Visibility.Collapsed;
                for (var i = 0; i < n; i++)
                    RunningAppsList.Items.Add(BuildInstalledRow(filtered[i], isLast: i == n - 1));
            }
            RunningAppsCount.Text = $"{n} installed app{(n == 1 ? "" : "s")}";
        }
        else
        {
            var filtered = ViewModel.RunningApps
                .Where(a => string.IsNullOrEmpty(_query)
                            || a.DisplayLabel.Contains(_query, StringComparison.OrdinalIgnoreCase)
                            || a.ProcessName.Contains(_query, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            n = filtered.Length;
            if (ViewModel.IsBusy)
            {
                RefreshingPanel.Visibility = Visibility.Visible;
                RunningEmpty.Visibility = Visibility.Collapsed;
            }
            else
            {
                RefreshingPanel.Visibility = Visibility.Collapsed;
                RunningEmpty.Visibility = n == 0 ? Visibility.Visible : Visibility.Collapsed;
                for (var i = 0; i < n; i++)
                    RunningAppsList.Items.Add(BuildRunningRow(filtered[i], isLast: i == n - 1));
            }
            RunningAppsCount.Text = $"{n} running app{(n == 1 ? "" : "s")}";
        }

        // Selected list (right column) — combine running + installed + manual
        var selectedRunning = ViewModel.RunningApps.Where(a => a.IsSelected).ToList();
        var selectedInstalled = ViewModel.InstalledApps.Where(a => a.IsSelected).ToList();
        var selectedCount = selectedRunning.Count + selectedInstalled.Count + ViewModel.ManualEntries.Count;
        SelectedBadgeText.Text = selectedCount.ToString();
        SelectedBadge.Style = (Style)Application.Current.Resources[selectedCount > 0 ? "StopMeBadgeAccent" : "StopMeBadge"];
        SelectedBadgeText.Foreground = (Brush)Application.Current.Resources[selectedCount > 0 ? "StopMeAccentTextBrush" : "StopMeTextSecondaryBrush"];
        SelectedEmpty.Visibility = selectedCount == 0 ? Visibility.Visible : Visibility.Collapsed;
        SelectedList.Items.Clear();
        foreach (var s in selectedRunning) SelectedList.Items.Add(BuildSelectedRow(s.ProcessName, source: "Running", remove: () => { s.IsSelected = false; RenderLists(); }));
        foreach (var s in selectedInstalled) SelectedList.Items.Add(BuildSelectedRow(s.DisplayName, source: "Installed", remove: () => { s.IsSelected = false; RenderLists(); }));
        foreach (var m in ViewModel.ManualEntries) SelectedList.Items.Add(BuildSelectedRow(m.ProcessName, source: "Manual", remove: () => ViewModel.RemoveManualEntryCommand.Execute(m)));

        UpdateTabStyles();

        // Sticky bar button label
        if (selectedCount == 0)
        {
            StartBlockingBtn.IsEnabled = false;
            StartBlockingText.Text = "Select apps to block";
        }
        else
        {
            StartBlockingBtn.IsEnabled = true;
            StartBlockingText.Text = $"Start blocking {selectedCount} app{(selectedCount == 1 ? "" : "s")}";
        }
    }

    private FrameworkElement BuildRunningRow(RunningAppItem app, bool isLast)
    {
        var checkedBrush = (Brush)Application.Current.Resources["StopMeAccentSoftBrush"];
        var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        var grid = new Grid
        {
            Padding = new Thickness(20, 12, 20, 12),
            Background = app.IsSelected ? checkedBrush : transparent,
            ColumnSpacing = 14,
            BorderBrush = (Brush)Application.Current.Resources["StopMeBorderBrush"],
            BorderThickness = new Thickness(0, 0, 0, isLast ? 0 : 1),
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tile = new AppTile { Label = ExtractDisplayName(app), TileSize = 36 };
        Grid.SetColumn(tile, 0);
        grid.Children.Add(tile);

        var nameStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        nameStack.Children.Add(new TextBlock
        {
            Text = ExtractDisplayName(app),
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        nameStack.Children.Add(new TextBlock
        {
            Text = app.ProcessName,
            FontFamily = (FontFamily)Application.Current.Resources["StopMeMonoFontFamily"],
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["StopMeTextTertiaryBrush"],
        });
        Grid.SetColumn(nameStack, 1);
        grid.Children.Add(nameStack);

        var label = new TextBlock
        {
            Text = app.IsSelected ? "Blocked during session" : "Block",
            FontSize = 12.5,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            Foreground = (Brush)Application.Current.Resources[app.IsSelected ? "StopMeAccentTextBrush" : "StopMeTextTertiaryBrush"],
        };
        Grid.SetColumn(label, 2);
        grid.Children.Add(label);

        var toggle = new ToggleSwitch
        {
            IsOn = app.IsSelected,
            OnContent = "",
            OffContent = "",
            MinWidth = 42,
            VerticalAlignment = VerticalAlignment.Center,
        };
        toggle.Toggled += (_, _) => { app.IsSelected = toggle.IsOn; RenderLists(); };
        Grid.SetColumn(toggle, 3);
        grid.Children.Add(toggle);

        var btn = new Button
        {
            Background = transparent,
            BorderBrush = transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(0),
            Padding = new Thickness(0),
            Content = grid,
        };
        btn.Click += (_, _) => { app.IsSelected = !app.IsSelected; RenderLists(); };
        return btn;
    }

    private FrameworkElement BuildSelectedRow(string processName, string source, Action remove)
    {
        var grid = new Grid { ColumnSpacing = 10, Padding = new Thickness(0, 7, 0, 7) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tile = new AppTile { Label = processName, TileSize = 28, TileRadius = 8 };
        Grid.SetColumn(tile, 0);
        grid.Children.Add(tile);

        var name = new TextBlock
        {
            Text = processName.Replace(".exe", string.Empty, StringComparison.OrdinalIgnoreCase),
            FontSize = 13.5,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var badgeBorder = new Border
        {
            Style = (Style)Application.Current.Resources["StopMeBadge"],
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = source,
                Style = (Style)Application.Current.Resources["StopMeBadgeText"],
                Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
            },
        };
        Grid.SetColumn(badgeBorder, 2);
        grid.Children.Add(badgeBorder);

        var removeBtn = new Button
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            CornerRadius = new CornerRadius(7),
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new IconGlyph { IconName = "x", Size = 14, ForegroundBrush = (Brush)Application.Current.Resources["StopMeTextTertiaryBrush"] },
        };
        removeBtn.Click += (_, _) => remove();
        Grid.SetColumn(removeBtn, 3);
        grid.Children.Add(removeBtn);

        return grid;
    }

    private FrameworkElement BuildInstalledRow(InstalledAppItem app, bool isLast)
    {
        var checkedBrush = (Brush)Application.Current.Resources["StopMeAccentSoftBrush"];
        var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        var grid = new Grid
        {
            Padding = new Thickness(20, 12, 20, 12),
            Background = app.IsSelected ? checkedBrush : transparent,
            ColumnSpacing = 14,
            BorderBrush = (Brush)Application.Current.Resources["StopMeBorderBrush"],
            BorderThickness = new Thickness(0, 0, 0, isLast ? 0 : 1),
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tile = new AppTile { Label = app.DisplayName, TileSize = 36 };
        Grid.SetColumn(tile, 0);
        grid.Children.Add(tile);

        var nameStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        nameStack.Children.Add(new TextBlock
        {
            Text = app.DisplayName,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        nameStack.Children.Add(new TextBlock
        {
            Text = string.IsNullOrEmpty(app.Publisher)
                ? (app.ProcessName ?? string.Empty)
                : $"{app.Publisher} · {app.ProcessName}",
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["StopMeTextTertiaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetColumn(nameStack, 1);
        grid.Children.Add(nameStack);

        var label = new TextBlock
        {
            Text = app.IsSelected ? "Will be blocked" : "Block",
            FontSize = 12.5,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
            Foreground = (Brush)Application.Current.Resources[app.IsSelected ? "StopMeAccentTextBrush" : "StopMeTextTertiaryBrush"],
        };
        Grid.SetColumn(label, 2);
        grid.Children.Add(label);

        var toggle = new ToggleSwitch
        {
            IsOn = app.IsSelected,
            OnContent = "",
            OffContent = "",
            MinWidth = 42,
            VerticalAlignment = VerticalAlignment.Center,
        };
        toggle.Toggled += (_, _) => { app.IsSelected = toggle.IsOn; RenderLists(); };
        Grid.SetColumn(toggle, 3);
        grid.Children.Add(toggle);

        var btn = new Button
        {
            Background = transparent,
            BorderBrush = transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(0),
            Padding = new Thickness(0),
            Content = grid,
        };
        btn.Click += (_, _) => { app.IsSelected = !app.IsSelected; RenderLists(); };
        return btn;
    }

    private static string ExtractDisplayName(RunningAppItem app)
    {
        var s = app.DisplayLabel.Split("  —  ").FirstOrDefault() ?? app.ProcessName;
        return s.Replace(".exe", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    // ===== Event handlers =====

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        RefreshLabel.Text = "Refreshing…";
        await ViewModel.RefreshCommand.ExecuteAsync(null);
        RefreshLabel.Text = "Refresh apps";
        RenderLists();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        _query = SearchBox.Text?.Trim() ?? string.Empty;
        RenderLists();
    }

    private void OnToggleManualClick(object sender, RoutedEventArgs e)
    {
        var open = ManualPanel.Visibility == Visibility.Visible;
        ManualPanel.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
        ManualHint.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        ManualToggleIcon.IconName = open ? "chevronRight" : "chevronDown";
    }

    private void OnAddManualClick(object sender, RoutedEventArgs e)
    {
        var name = !string.IsNullOrWhiteSpace(ManualProcessBox.Text) ? ManualProcessBox.Text
                 : !string.IsNullOrWhiteSpace(ManualPathBox.Text)    ? System.IO.Path.GetFileName(ManualPathBox.Text)
                 : !string.IsNullOrWhiteSpace(ManualFolderBox.Text)  ? System.IO.Path.GetFileName(ManualFolderBox.Text)
                 : ManualTitleBox.Text;
        if (string.IsNullOrWhiteSpace(name)) return;
        ViewModel.ManualProcessName = name.Trim();
        ViewModel.AddManualEntryCommand.Execute(null);
        ManualProcessBox.Text = ManualPathBox.Text = ManualFolderBox.Text = ManualTitleBox.Text = string.Empty;
        ManualPanel.Visibility = Visibility.Collapsed;
        ManualHint.Visibility = Visibility.Visible;
        ManualToggleIcon.IconName = "chevronRight";
        RenderLists();
    }

    private async void OnStartClick(object sender, RoutedEventArgs e)
    {
        if (StickyStrict.IsStrict)
        {
            var dialog = new ContentDialog
            {
                Title = "Start a strict session?",
                Content = new TextBlock
                {
                    Text = "Strict mode can't be stopped before the timer ends. "
                           + "Not by closing StopMe, not by restarting Windows.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
                },
                PrimaryButtonText = "I'm sure — lock me in",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };
            var r = await dialog.ShowAsync();
            if (r != ContentDialogResult.Primary) return;
        }
        StartBlockingText.Text = "Starting…";
        await ViewModel.StartBlockingCommand.ExecuteAsync(null);
        RenderLists();
        if (!string.IsNullOrEmpty(ViewModel.LastStartSummary))
        {
            StatusBanner.Visibility = Visibility.Visible;
            StatusBannerText.Text = ViewModel.LastStartSummary;
            _window.ShowToast(ViewModel.LastStartSummary);
        }
        else if (!string.IsNullOrEmpty(ViewModel.StatusMessage))
        {
            StatusBanner.Visibility = Visibility.Visible;
            StatusBannerText.Text = ViewModel.StatusMessage;
        }
    }
}
