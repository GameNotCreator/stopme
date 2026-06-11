using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace StopMe.App.Controls;

// Duration picker — 15 min / 30 min / 1 h / 2 h / Custom (per the spec).
// Renders as pill buttons; selected pill uses accent-soft + accent border + accent-text.
public sealed partial class DurationSelector : UserControl
{
    public static readonly DependencyProperty MinutesProperty =
        DependencyProperty.Register(nameof(Minutes), typeof(int), typeof(DurationSelector),
            new PropertyMetadata(60, OnMinutesChanged));

    public static readonly DependencyProperty CompactProperty =
        DependencyProperty.Register(nameof(Compact), typeof(bool), typeof(DurationSelector),
            new PropertyMetadata(false, OnCompactChanged));

    private static readonly (string Label, int Mins)[] Presets =
    {
        ("15 min", 15),
        ("30 min", 30),
        ("1 h",    60),
        ("2 h",    120),
    };

    public event EventHandler<int>? MinutesChanged;

    public DurationSelector()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
    }

    public int Minutes
    {
        get => (int)GetValue(MinutesProperty);
        set => SetValue(MinutesProperty, value);
    }

    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    private static void OnMinutesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var s = (DurationSelector)d;
        s.Render();
        if (e.OldValue is int oldV && e.NewValue is int newV && oldV != newV)
            s.MinutesChanged?.Invoke(s, newV);
    }

    private static void OnCompactChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((DurationSelector)d).Render();

    private void Render()
    {
        Root.Children.Clear();
        var isPreset = false;
        foreach (var (label, mins) in Presets)
        {
            var selected = mins == Minutes;
            if (selected) isPreset = true;
            Root.Children.Add(MakePill(label, selected, () => Minutes = mins));
        }
        // Custom button — when current Minutes isn't a preset, show its value as the label.
        var customLabel = isPreset ? "Custom" : $"{Minutes} min";
        Root.Children.Add(MakePill(customLabel, selected: !isPreset, OnCustomClicked, dashed: isPreset));
    }

    private Button MakePill(string label, bool selected, Action onClick, bool dashed = false)
    {
        var btn = new Button
        {
            Content = new TextBlock { Text = label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = Compact ? 13 : 13.5 },
            Padding = Compact ? new Thickness(13, 7, 13, 7) : new Thickness(16, 9, 16, 9),
            CornerRadius = new CornerRadius(999),
            MinHeight = Compact ? 30 : 38,
            BorderThickness = new Thickness(1),
        };
        if (selected)
        {
            btn.Background = (Brush)Application.Current.Resources["StopMeAccentSoftBrush"];
            btn.BorderBrush = (Brush)Application.Current.Resources["StopMeAccentBrush"];
            btn.Foreground = (Brush)Application.Current.Resources["StopMeAccentTextBrush"];
        }
        else if (dashed)
        {
            btn.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            btn.BorderBrush = (Brush)Application.Current.Resources["StopMeBorderStrongBrush"];
            btn.Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"];
        }
        else
        {
            btn.Background = (Brush)Application.Current.Resources["StopMeBgPrimaryBrush"];
            btn.BorderBrush = (Brush)Application.Current.Resources["StopMeBorderStrongBrush"];
            btn.Foreground = (Brush)Application.Current.Resources["StopMeTextPrimaryBrush"];
        }
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private async void OnCustomClicked()
    {
        // The prototype opens an inline text input. For the WinUI port we use a
        // lightweight ContentDialog so it works without a custom popup harness.
        var input = new TextBox
        {
            Text = Minutes.ToString(),
            Width = 120,
            PlaceholderText = "Minutes",
            FontFamily = (FontFamily)Application.Current.Resources["StopMeSansFontFamily"],
        };
        var dialog = new ContentDialog
        {
            Title = "Custom duration",
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = "How many minutes should this session last?",
                        Style = (Style)Application.Current.Resources["StopMeBodySecondary"],
                    },
                    input,
                },
            },
            PrimaryButtonText = "Use",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && int.TryParse(input.Text, out var v) && v > 0)
        {
            Minutes = v;
        }
    }
}
