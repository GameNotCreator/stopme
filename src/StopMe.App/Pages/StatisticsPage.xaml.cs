using System;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StopMe.App.Controls;
using StopMe.App.Core.ViewModels;
using Windows.UI;

namespace StopMe.App.Pages;

public sealed partial class StatisticsPage : Page
{
    public StatisticsViewModel ViewModel { get; }

    public StatisticsPage(StatisticsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await ViewModel.RefreshAsync();
            RenderAll();
        };
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ViewModel.FocusMinutes)
                or nameof(ViewModel.SessionsCompleted)
                or nameof(ViewModel.AppBlockCount)
                or nameof(ViewModel.WebsiteBlockCount)
                or nameof(ViewModel.ProductiveDayStreak))
            {
                DispatcherQueue.TryEnqueue(RenderAll);
            }
        };
    }

    // ===== Render =====

    private void RenderAll()
    {
        var sub = ViewModel.SelectedPeriodDescription.ToLowerInvariant();

        FocusTimeValue.Text = FormatHM(ViewModel.FocusMinutes);
        FocusTimeSub.Text = sub;
        SessionsValue.Text = ViewModel.SessionsCompleted.ToString();
        StreakValue.Text = $"{ViewModel.ProductiveDayStreak} days";
        AppBlocksValue.Text = ViewModel.AppBlockCount.ToString();
        SiteBlocksValue.Text = ViewModel.WebsiteBlockCount.ToString();

        var top = ViewModel.TopDistractions.FirstOrDefault();
        TopDistractionValue.Text = top?.Label ?? "—";
        TopDistractionSub.Text = top is null ? string.Empty : $"{top.BlockCount} blocked attempts";

        RenderFocusByDay();
        RenderCategoryBar();
        RenderTopBlocked();

        if (ViewModel.HasError)
        {
            ErrorCard.Visibility = Visibility.Visible;
            ErrorText.Text = ViewModel.StatusMessage ?? "Unknown error";
        }
        else
        {
            ErrorCard.Visibility = Visibility.Collapsed;
        }
    }

    private void RenderFocusByDay()
    {
        FocusByDayBars.Children.Clear();
        FocusByDayBars.ColumnDefinitions.Clear();

        var buckets = ViewModel.Daily.ToArray();
        if (buckets.Length == 0)
        {
            // Demo fallback when no data — keep the layout legible.
            buckets = new[]
            {
                CreateDemoBucket("Mon", 150),
                CreateDemoBucket("Tue", 210),
                CreateDemoBucket("Wed", 90),
                CreateDemoBucket("Thu", 240),
                CreateDemoBucket("Fri", 180),
                CreateDemoBucket("Sat", 0),
                CreateDemoBucket("Sun", 60),
            };
        }

        var maxMins = Math.Max(1, buckets.Max(b => b.FocusMinutes));
        for (var i = 0; i < buckets.Length; i++)
            FocusByDayBars.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (var i = 0; i < buckets.Length; i++)
        {
            var b = buckets[i];
            var col = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 8 };
            col.Children.Add(new TextBlock
            {
                Text = b.FocusMinutes == 0 ? "—" : $"{Math.Round(b.FocusMinutes / 60.0, 1)}h",
                FontSize = 11.5,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = (Brush)Application.Current.Resources[b.FocusMinutes == 0 ? "StopMeTextTertiaryBrush" : "StopMeAccentTextBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            var heightPx = b.FocusMinutes == 0 ? 4.0 : Math.Max(4.0, (b.FocusMinutes / (double)maxMins) * 120);
            col.Children.Add(new Border
            {
                Width = 44,
                Height = heightPx,
                CornerRadius = new CornerRadius(8),
                Background = (Brush)Application.Current.Resources[b.FocusMinutes == 0 ? "StopMeBgTertiaryBrush" : "StopMeAccentBrush"],
                Opacity = b.FocusMinutes == 0 ? 1.0 : 0.5 + 0.5 * (b.FocusMinutes / (double)maxMins),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            col.Children.Add(new TextBlock
            {
                Text = LabelFor(b),
                FontSize = 11.5,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            Grid.SetColumn(col, i);
            FocusByDayBars.Children.Add(col);
        }
    }

    private static string LabelFor(StopMe.LocalApi.Dtos.StatsDailyBucketDto b)
    {
        // Date arrives as ISO yyyy-MM-dd. Convert to short weekday name.
        if (DateTime.TryParse(b.Date, out var d))
            return d.ToString("ddd");
        return b.Date.Length >= 3 ? b.Date.Substring(0, 3) : b.Date;
    }

    private static StopMe.LocalApi.Dtos.StatsDailyBucketDto CreateDemoBucket(string label, int focusMins)
    {
        // Demo path only fires when the service returned an empty list — we
        // synthesize a 7-day window ending today so the chart still reads correctly.
        var today = DateTime.Today;
        var dow = (int)today.DayOfWeek;
        var dayIndex = Array.IndexOf(new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" }, label);
        var d = today.AddDays(dayIndex - dow);
        return new StopMe.LocalApi.Dtos.StatsDailyBucketDto
        {
            Date = d.ToString("yyyy-MM-dd"),
            FocusMinutes = focusMins,
            SessionsCompleted = 0,
            AppBlockCount = 0,
            WebsiteBlockCount = 0,
        };
    }

    private void RenderCategoryBar()
    {
        // Demo data — the service doesn't expose grouped category counts yet.
        var data = new (string Label, int Count, string ColorKey)[]
        {
            ("Video Streaming", 212, "StopMeColorAccent"),
            ("Social Media",    148, "StopMeChart2Brush"),
            ("Gaming",           86, "StopMeChart3Brush"),
            ("Other",            43, "StopMeChart5Brush"),
        };
        var total = data.Sum(d => d.Count);
        CategorySegmented.Children.Clear();
        CategorySegmented.ColumnDefinitions.Clear();
        for (var i = 0; i < data.Length; i++)
        {
            CategorySegmented.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(data[i].Count, GridUnitType.Star) });
            Brush brush = data[i].ColorKey == "StopMeColorAccent"
                ? (Brush)Application.Current.Resources["StopMeAccentBrush"]
                : (Brush)Application.Current.Resources[data[i].ColorKey];
            var seg = new Border { Background = brush, Margin = new Thickness(i == 0 ? 0 : 2, 0, 0, 0) };
            Grid.SetColumn(seg, i);
            CategorySegmented.Children.Add(seg);
        }

        CategoryLegend.Items.Clear();
        foreach (var (label, count, colorKey) in data)
        {
            var row = new Grid { ColumnSpacing = 9, Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Brush brush = colorKey == "StopMeColorAccent"
                ? (Brush)Application.Current.Resources["StopMeAccentBrush"]
                : (Brush)Application.Current.Resources[colorKey];

            var swatch = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(3), Background = brush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(swatch, 0);
            row.Children.Add(swatch);

            var lbl = new TextBlock
            {
                Text = label,
                Style = (Style)Application.Current.Resources["StopMeBodySecondary"],
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(lbl, 1);
            row.Children.Add(lbl);

            var num = new TextBlock
            {
                Text = count.ToString(),
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(num, 2);
            row.Children.Add(num);

            CategoryLegend.Items.Add(row);
        }
    }

    private void RenderTopBlocked()
    {
        TopBlockedList.Items.Clear();
        var top = ViewModel.TopDistractions.Take(5).ToArray();
        if (top.Length == 0)
        {
            // Demo fallback
            var demo = new (string Name, string Kind, int Count, string Color)[]
            {
                ("YouTube",   "site", 134, "#FF0033"),
                ("Discord",   "app",   89, "#5865F2"),
                ("Reddit",    "site",  67, "#FF4500"),
                ("Steam",     "app",   41, "#1B2838"),
                ("Instagram", "site",  38, "#C13584"),
            };
            for (var i = 0; i < demo.Length; i++)
                TopBlockedList.Items.Add(BuildTopRow(i + 1, demo[i].Name, demo[i].Kind, demo[i].Count, demo[i].Color));
        }
        else
        {
            for (var i = 0; i < top.Length; i++)
            {
                var t = top[i];
                TopBlockedList.Items.Add(BuildTopRow(i + 1, t.Label, t.Kind?.ToLowerInvariant() ?? "—", t.BlockCount, null));
            }
        }
    }

    private FrameworkElement BuildTopRow(int rank, string name, string kind, int count, string? colorHex)
    {
        var grid = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 0, 0, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var rankText = new TextBlock
        {
            Text = rank.ToString(),
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = (Brush)Application.Current.Resources["StopMeTextTertiaryBrush"],
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(rankText, 0);
        grid.Children.Add(rankText);

        var tile = new AppTile { Label = name, TileSize = 26, TileRadius = 8, TileColor = colorHex ?? string.Empty };
        Grid.SetColumn(tile, 1);
        grid.Children.Add(tile);

        var nameBlock = new TextBlock
        {
            Text = name,
            FontSize = 13.5,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(nameBlock, 2);
        grid.Children.Add(nameBlock);

        var badge = new Border
        {
            Style = (Style)Application.Current.Resources["StopMeBadge"],
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = kind,
                Style = (Style)Application.Current.Resources["StopMeBadgeText"],
                Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
            },
        };
        Grid.SetColumn(badge, 3);
        grid.Children.Add(badge);

        var countText = new TextBlock
        {
            Text = count.ToString(),
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        Grid.SetColumn(countText, 4);
        grid.Children.Add(countText);

        return grid;
    }

    // ===== User actions =====

    private async void OnPeriodChanged(object sender, SelectionChangedEventArgs e)
    {
        await ViewModel.RefreshCommand.ExecuteAsync(null);
        RenderAll();
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ExportCsvCommand.ExecuteAsync(null);
        ((MainWindow)App.MainWindow!).ShowToast(ViewModel.StatusMessage ?? "Statistics exported.");
    }

    private async void OnResetClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Reset statistics?",
            Content = new TextBlock
            {
                Text = "This will permanently delete every usage event. Blocklists and sessions are kept.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
            },
            PrimaryButtonText = "Reset",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        var r = await dialog.ShowAsync();
        if (r != ContentDialogResult.Primary) return;
        await ViewModel.ResetCommand.ExecuteAsync(null);
        RenderAll();
    }

    private static string FormatHM(int mins)
    {
        if (mins <= 0) return "0 min";
        var h = mins / 60;
        var m = mins % 60;
        return h == 0 ? $"{m} min" : (m == 0 ? $"{h} h" : $"{h} h {m} min");
    }
}
