using System;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using StopMe.App.Controls;
using StopMe.App.Core.ViewModels;
using StopMe.LocalApi.Dtos;
using Windows.System;

namespace StopMe.App.Pages;

public sealed partial class BlockWebsitesPage : Page
{
    private static readonly string[] ExampleSites =
    {
        "youtube.com", "instagram.com", "reddit.com/r/funny", "google.com/*q=*game*",
    };

    private readonly MainWindow _window;

    public BlockWebsitesViewModel ViewModel { get; }

    public BlockWebsitesPage(BlockWebsitesViewModel viewModel, MainWindow window)
    {
        ViewModel = viewModel;
        _window = window;
        InitializeComponent();

        ViewModel.SelectedSites.CollectionChanged += (_, _) => RenderSites();
        ViewModel.SelectedCategories.CollectionChanged += (_, _) => { RenderCategories(); RenderSites(); };
        ViewModel.Exceptions.CollectionChanged += (_, _) => RenderExceptions();
        ViewModel.SearchResults.CollectionChanged += (_, _) => RenderSearchResults();

        StickyDuration.MinutesChanged += (_, m) => ViewModel.DurationMinutes = m;
        StickyStrict.Toggled += (_, on) => ViewModel.SelectedMode = on ? "Strict" : "Soft";

        Loaded += (_, _) =>
        {
            StickyDuration.Minutes = ViewModel.DurationMinutes;
            StickyStrict.IsStrict = string.Equals(ViewModel.SelectedMode, "Strict", StringComparison.OrdinalIgnoreCase);
            RenderExamples();
            RenderCategories();
            RenderSites();
            RenderExceptions();
        };
    }

    private void RenderExamples()
    {
        ExampleChips.Items.Clear();
        foreach (var ex in ExampleSites)
        {
            var btn = new Button
            {
                Background = (Brush)Application.Current.Resources["StopMeBgSecondaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources["StopMeBorderBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(99),
                Padding = new Thickness(10, 3, 10, 3),
                MinHeight = 24,
                Content = new TextBlock
                {
                    Text = ex,
                    FontSize = 12,
                    FontFamily = (FontFamily)Application.Current.Resources["StopMeMonoFontFamily"],
                    Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
                },
            };
            btn.Click += (_, _) => SiteInputBox.Text = ex;
            ExampleChips.Items.Add(btn);
        }
    }

    private void RenderCategories()
    {
        CategoryChips.Items.Clear();
        foreach (var cat in ViewModel.Categories)
        {
            var selected = ViewModel.SelectedCategories.Contains(cat.Id);
            var pill = new Button
            {
                Background = (Brush)Application.Current.Resources[selected ? "StopMeAccentSoftBrush" : "StopMeBgPrimaryBrush"],
                BorderBrush = (Brush)Application.Current.Resources[selected ? "StopMeAccentBrush" : "StopMeBorderStrongBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(0, 0, 8, 8),
                Foreground = (Brush)Application.Current.Resources[selected ? "StopMeAccentTextBrush" : "StopMeTextPrimaryBrush"],
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new IconGlyph
            {
                IconName = cat.Icon,
                Size = 15,
                ForegroundBrush = pill.Foreground,
            });
            stack.Children.Add(new TextBlock
            {
                Text = cat.Label,
                FontSize = 13.5,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = pill.Foreground,
            });
            stack.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources[selected ? "StopMeAccentSoft2Brush" : "StopMeBgTertiaryBrush"],
                CornerRadius = new CornerRadius(99),
                Padding = new Thickness(7, 1, 7, 1),
                Child = new TextBlock
                {
                    Text = cat.Sites.Count.ToString(),
                    FontSize = 11,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    Foreground = (Brush)Application.Current.Resources[selected ? "StopMeAccentTextBrush" : "StopMeTextSecondaryBrush"],
                },
            });
            pill.Content = stack;
            pill.Click += (_, _) => ViewModel.ToggleCategoryCommand.Execute(cat.Id);
            CategoryChips.Items.Add(pill);
        }

        var covered = ViewModel.CategorySiteCount;
        if (covered > 0)
        {
            CategoryCoveredHint.Visibility = Visibility.Visible;
            CategoryCoveredText.Text = $"{covered} sites covered by categories";
        }
        else
        {
            CategoryCoveredHint.Visibility = Visibility.Collapsed;
        }
    }

    private void RenderSites()
    {
        SitesList.Items.Clear();
        var n = ViewModel.SelectedSites.Count;
        SitesBadgeText.Text = n.ToString();
        SitesBadge.Style = (Style)Application.Current.Resources[n > 0 ? "StopMeBadgeAccent" : "StopMeBadge"];
        SitesBadgeText.Foreground = (Brush)Application.Current.Resources[n > 0 ? "StopMeAccentTextBrush" : "StopMeTextSecondaryBrush"];
        SitesEmpty.Visibility = n == 0 ? Visibility.Visible : Visibility.Collapsed;

        for (var i = 0; i < n; i++)
            SitesList.Items.Add(BuildSiteRow(ViewModel.SelectedSites[i], isLast: i == n - 1));

        UpdateStartButton();
    }

    private FrameworkElement BuildSiteRow(string site, bool isLast)
    {
        var grid = new Grid
        {
            Padding = new Thickness(20, 10, 20, 10),
            BorderBrush = (Brush)Application.Current.Resources["StopMeBorderBrush"],
            BorderThickness = new Thickness(0, 0, 0, isLast ? 0 : 1),
            ColumnSpacing = 12,
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tile = new AppTile { Label = site, TileSize = 28, TileRadius = 8 };
        Grid.SetColumn(tile, 0);
        grid.Children.Add(tile);

        var name = new TextBlock
        {
            Text = site,
            FontSize = 13.5,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = site.Contains('*') ? (FontFamily)Application.Current.Resources["StopMeMonoFontFamily"] : null,
        };
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        if (site.Contains('*'))
        {
            var badge = new Border
            {
                Style = (Style)Application.Current.Resources["StopMeBadge"],
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "Wildcard",
                    Style = (Style)Application.Current.Resources["StopMeBadgeText"],
                    Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
                },
            };
            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);
        }

        var remove = new Button
        {
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(Colors.Transparent),
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(7),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new IconGlyph { IconName = "x", Size = 14, ForegroundBrush = (Brush)Application.Current.Resources["StopMeTextTertiaryBrush"] },
        };
        remove.Click += (_, _) => ViewModel.RemoveSiteCommand.Execute(site);
        Grid.SetColumn(remove, 3);
        grid.Children.Add(remove);

        return grid;
    }

    private void RenderExceptions()
    {
        ExceptionChipsList.Items.Clear();
        foreach (var x in ViewModel.Exceptions)
        {
            var chip = new Border
            {
                Background = (Brush)Application.Current.Resources["StopMeSuccessSoftBrush"],
                CornerRadius = new CornerRadius(99),
                Padding = new Thickness(11, 4, 6, 4),
                Margin = new Thickness(0, 0, 6, 6),
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = x,
                FontSize = 12.5,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["StopMeSuccessBrush"],
                VerticalAlignment = VerticalAlignment.Center,
            });
            var remove = new Button
            {
                Background = new SolidColorBrush(Colors.Transparent),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                Padding = new Thickness(2),
                Content = new IconGlyph { IconName = "x", Size = 12, ForegroundBrush = (Brush)Application.Current.Resources["StopMeSuccessBrush"] },
            };
            remove.Click += (_, _) => ViewModel.RemoveExceptionCommand.Execute(x);
            stack.Children.Add(remove);
            chip.Child = stack;
            ExceptionChipsList.Items.Add(chip);
        }
    }

    private void UpdateStartButton()
    {
        var total = ViewModel.TotalCount;
        StartBlockingBtn.IsEnabled = total > 0;
        StartBlockingText.Text = total == 0
            ? "Select websites to block"
            : $"Start blocking {total} website{(total == 1 ? "" : "s")}";
    }

    private void OnSiteInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            ViewModel.SiteInput = SiteInputBox.Text;
            ViewModel.AddSiteCommand.Execute(null);
            SiteInputBox.Text = string.Empty;
        }
    }

    private void OnAddSiteClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SiteInput = SiteInputBox.Text;
        ViewModel.AddSiteCommand.Execute(null);
        SiteInputBox.Text = string.Empty;
    }

    private void OnExceptionInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            ViewModel.ExceptionInput = ExceptionBox.Text;
            ViewModel.AddExceptionCommand.Execute(null);
            ExceptionBox.Text = string.Empty;
        }
    }

    private void OnAddExceptionClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ExceptionInput = ExceptionBox.Text;
        ViewModel.AddExceptionCommand.Execute(null);
        ExceptionBox.Text = string.Empty;
    }

    private async void OnSearchClick(object sender, RoutedEventArgs e) =>
        await RunSearchAsync();

    private async void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter) await RunSearchAsync();
    }

    private async Task RunSearchAsync()
    {
        ViewModel.SearchQuery = SearchBox.Text;
        SearchButtonText.Text = "Searching…";
        try
        {
            await ViewModel.SearchCommand.ExecuteAsync(null);
            RenderSearchResults();
        }
        finally
        {
            SearchButtonText.Text = "Search";
        }
    }

    private void RenderSearchResults()
    {
        SearchResultsList.Items.Clear();
        foreach (var hit in ViewModel.SearchResults)
            SearchResultsList.Items.Add(BuildSearchRow(hit));
        if (ViewModel.SearchResults.Count == 0 && !string.IsNullOrEmpty(ViewModel.StatusMessage))
        {
            SearchResultsList.Items.Add(new TextBlock
            {
                Text = ViewModel.StatusMessage,
                FontSize = 12.5,
                Foreground = (Brush)Application.Current.Resources["StopMeTextTertiaryBrush"],
                Margin = new Thickness(0, 6, 0, 0),
            });
        }
    }

    private FrameworkElement BuildSearchRow(DomainSearchHitDto hit)
    {
        var grid = new Grid
        {
            ColumnSpacing = 12,
            Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 4, 0, 0),
            CornerRadius = new CornerRadius(12),
            Background = (Brush)Application.Current.Resources["StopMeBgSecondaryBrush"],
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var tile = new AppTile { Label = hit.Domain, TileSize = 28, TileRadius = 8 };
        Grid.SetColumn(tile, 0);
        grid.Children.Add(tile);

        var stack = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new TextBlock
        {
            Text = hit.Domain,
            FontSize = 13.5,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontFamily = (FontFamily)Application.Current.Resources["StopMeMonoFontFamily"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        stack.Children.Add(new TextBlock
        {
            Text = hit.Title,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetColumn(stack, 1);
        grid.Children.Add(stack);

        var addBtn = new Button
        {
            Style = (Style)Application.Current.Resources["StopMeSoftButton"],
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new IconGlyph { IconName = "plus", Size = 14, ForegroundBrush = (Brush)Application.Current.Resources["StopMeAccentTextBrush"] },
                    new TextBlock { Text = "Add", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                },
            },
        };
        addBtn.Click += (_, _) => ViewModel.AddFromSearchCommand.Execute(hit);
        Grid.SetColumn(addBtn, 2);
        grid.Children.Add(addBtn);
        return grid;
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
        UpdateStartButton();
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
