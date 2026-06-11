using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace StopMe.App.Controls;

public sealed partial class LogoTile : UserControl
{
    public static readonly DependencyProperty TileSizeProperty =
        DependencyProperty.Register(nameof(TileSize), typeof(double), typeof(LogoTile),
            new PropertyMetadata(30.0, OnTileSizeChanged));

    public LogoTile()
    {
        InitializeComponent();
        ApplySize();
    }

    public double TileSize
    {
        get => (double)GetValue(TileSizeProperty);
        set => SetValue(TileSizeProperty, value);
    }

    private static void OnTileSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((LogoTile)d).ApplySize();

    private void ApplySize()
    {
        Root.Width = TileSize;
        Root.Height = TileSize;
        TileBorder.CornerRadius = new CornerRadius(System.Math.Round(TileSize * 0.30));
        ShieldIcon.FontSize = System.Math.Round(TileSize * 0.55);
    }
}
