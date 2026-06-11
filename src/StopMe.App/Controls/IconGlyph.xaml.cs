using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace StopMe.App.Controls;

// Lightweight icon helper. Names mirror the prototype's <Icon name="…"> values
// (Lucide-style names). We render them with Segoe MDL2 / Fluent System Icons —
// the spec explicitly allows substituting the platform icon set as long as
// the semantic mapping is preserved.
//
// NB: the public property is IconName (not Name) so it doesn't shadow the
// inherited FrameworkElement.Name (which is also bound by XAML's x:Name
// attribute — hiding it confused the XamlCompiler).
public sealed partial class IconGlyph : UserControl
{
    public static readonly DependencyProperty IconNameProperty =
        DependencyProperty.Register(nameof(IconName), typeof(string), typeof(IconGlyph),
            new PropertyMetadata("info", OnVisualChanged));

    public static readonly DependencyProperty SizeProperty =
        DependencyProperty.Register(nameof(Size), typeof(double), typeof(IconGlyph),
            new PropertyMetadata(18.0, OnVisualChanged));

    public static readonly DependencyProperty ForegroundBrushProperty =
        DependencyProperty.Register(nameof(ForegroundBrush), typeof(Brush), typeof(IconGlyph),
            new PropertyMetadata(null, OnVisualChanged));

    // name -> Segoe MDL2 glyph codepoint (UTF-8 inlined)
    private static readonly Dictionary<string, string> Glyphs = new()
    {
        // Navigation icons
        ["dashboard"]    = "", // Home
        ["apps"]         = "", // AllApps
        ["globe"]        = "", // World
        ["sessions"]     = "", // Calendar
        ["stats"]        = "", // BarChart4
        ["settings"]     = "", // Settings

        // Status / state
        ["lock"]         = "", // Lock
        ["unlock"]       = "", // Unlock
        ["play"]         = "", // Play
        ["stop"]         = "", // Stop
        ["pause"]        = "", // Pause
        ["check"]        = "", // CheckMark
        ["x"]            = "", // Cancel
        ["alert"]        = "", // Warning
        ["info"]         = "", // Info
        ["shield"]       = "", // ShieldSolid
        ["shieldCheck"]  = "",
        ["clock"]        = "", // Clock
        ["history"]      = "", // History
        ["repeat"]       = "", // Sync
        ["refresh"]      = "", // Refresh
        ["sparkle"]      = "", // FavoriteStar
        ["zap"]          = "", // LightningBolt
        ["help"]         = "", // Help
        ["folder"]       = "", // Folder
        ["file"]         = "", // Document
        ["edit"]         = "", // Edit
        ["trash"]        = "", // Delete
        ["external"]     = "", // OpenInNewWindow
        ["download"]     = "", // Download
        ["search"]       = "", // Search
        ["plus"]         = "", // Add
        ["minus"]        = "", // Subtract
        ["maximize"]     = "", // FullScreen
        ["moon"]         = "", // BrightnessLow
        ["chevronRight"] = "", // ChevronRight
        ["chevronDown"]  = "", // ChevronDown
        ["monitor"]      = "", // TVMonitor
        ["puzzle"]       = "", // Puzzle
        ["flame"]        = "", // FlameSolid
    };

    public IconGlyph()
    {
        InitializeComponent();
        Apply();
    }

    public string IconName
    {
        get => (string)GetValue(IconNameProperty);
        set => SetValue(IconNameProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public Brush? ForegroundBrush
    {
        get => (Brush?)GetValue(ForegroundBrushProperty);
        set => SetValue(ForegroundBrushProperty, value);
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((IconGlyph)d).Apply();

    private void Apply()
    {
        if (Glyphs.TryGetValue(IconName ?? string.Empty, out var g)) Glyph.Glyph = g;
        else Glyph.Glyph = ""; // info fallback
        Glyph.FontSize = Size;
        if (ForegroundBrush is not null) Glyph.Foreground = ForegroundBrush;
    }
}
