using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace StopMe.App.Controls;

// Colored letter-tile placeholder used wherever a real app or website icon
// would go (prototype's <AppTile>). In production we'd fetch the executable
// icon / favicon; this is the same fallback the prototype ships with.
public sealed partial class AppTile : UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(AppTile),
            new PropertyMetadata("?", OnVisualChanged));

    public static readonly DependencyProperty TileColorProperty =
        DependencyProperty.Register(nameof(TileColor), typeof(string), typeof(AppTile),
            new PropertyMetadata("", OnVisualChanged));

    public static readonly DependencyProperty TileSizeProperty =
        DependencyProperty.Register(nameof(TileSize), typeof(double), typeof(AppTile),
            new PropertyMetadata(36.0, OnVisualChanged));

    public static readonly DependencyProperty TileRadiusProperty =
        DependencyProperty.Register(nameof(TileRadius), typeof(double), typeof(AppTile),
            new PropertyMetadata(double.NaN, OnVisualChanged));

    public AppTile()
    {
        InitializeComponent();
        Apply();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string TileColor
    {
        get => (string)GetValue(TileColorProperty);
        set => SetValue(TileColorProperty, value);
    }

    public double TileSize
    {
        get => (double)GetValue(TileSizeProperty);
        set => SetValue(TileSizeProperty, value);
    }

    public double TileRadius
    {
        get => (double)GetValue(TileRadiusProperty);
        set => SetValue(TileRadiusProperty, value);
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AppTile)d).Apply();

    private void Apply()
    {
        var size = TileSize;
        Width = size;
        Height = size;
        TileBorder.Width = size;
        TileBorder.Height = size;
        TileBorder.CornerRadius = new CornerRadius(double.IsNaN(TileRadius) ? System.Math.Round(size * 0.28) : TileRadius);

        var letter = string.IsNullOrEmpty(Label) ? "?" : Label[..1].ToUpperInvariant();
        Letter.Text = letter;
        Letter.FontSize = System.Math.Round(size * 0.42);

        if (TryParseColor(TileColor, out var c))
        {
            TileBorder.Background = new SolidColorBrush(c);
        }
        else
        {
            // Derive a stable colour from the letter so different apps still
            // look visually distinct even without an explicit colour.
            var palette = new[]
            {
                Color.FromArgb(255, 0x58, 0x65, 0xF2), // discord blue
                Color.FromArgb(255, 0x1D, 0xB9, 0x54), // spotify green
                Color.FromArgb(255, 0xDB, 0x44, 0x37), // chrome red
                Color.FromArgb(255, 0x61, 0x1F, 0x69), // slack purple
                Color.FromArgb(255, 0x25, 0xD3, 0x66), // whatsapp
                Color.FromArgb(255, 0xFF, 0x00, 0x33), // youtube
                Color.FromArgb(255, 0xC1, 0x35, 0x84), // instagram
                Color.FromArgb(255, 0xFF, 0x45, 0x00), // reddit
                Color.FromArgb(255, 0x10, 0xA3, 0x7F), // chatgpt
            };
            var key = string.IsNullOrEmpty(Label) ? "?" : Label;
            var idx = System.Math.Abs(key.GetHashCode()) % palette.Length;
            TileBorder.Background = new SolidColorBrush(palette[idx]);
        }
    }

    private static bool TryParseColor(string? s, out Color c)
    {
        c = Colors.Transparent;
        if (string.IsNullOrEmpty(s)) return false;
        var t = s.Trim();
        if (t.StartsWith('#')) t = t[1..];
        if (t.Length == 6)
        {
            try
            {
                c = Color.FromArgb(255,
                    System.Convert.ToByte(t[..2], 16),
                    System.Convert.ToByte(t.Substring(2, 2), 16),
                    System.Convert.ToByte(t.Substring(4, 2), 16));
                return true;
            }
            catch { return false; }
        }
        if (t.Length == 8)
        {
            try
            {
                c = Color.FromArgb(
                    System.Convert.ToByte(t[..2], 16),
                    System.Convert.ToByte(t.Substring(2, 2), 16),
                    System.Convert.ToByte(t.Substring(4, 2), 16),
                    System.Convert.ToByte(t.Substring(6, 2), 16));
                return true;
            }
            catch { return false; }
        }
        return false;
    }
}
