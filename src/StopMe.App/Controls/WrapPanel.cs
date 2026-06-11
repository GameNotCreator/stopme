using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace StopMe.App.Controls;

// Minimal WrapPanel for WinUI 3 — WinUI doesn't ship one out of the box and we
// only need horizontal flow with per-child spacing.
public sealed class WrapPanel : Panel
{
    public static readonly DependencyProperty HorizontalSpacingProperty =
        DependencyProperty.Register(nameof(HorizontalSpacing), typeof(double), typeof(WrapPanel),
            new PropertyMetadata(8.0, OnSpacingChanged));

    public static readonly DependencyProperty VerticalSpacingProperty =
        DependencyProperty.Register(nameof(VerticalSpacing), typeof(double), typeof(WrapPanel),
            new PropertyMetadata(8.0, OnSpacingChanged));

    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    private static void OnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((WrapPanel)d).InvalidateMeasure();

    protected override Size MeasureOverride(Size availableSize)
    {
        var maxWidth = double.IsInfinity(availableSize.Width) ? 4000 : availableSize.Width;
        var hs = HorizontalSpacing;
        var vs = VerticalSpacing;
        double rowX = 0, rowHeight = 0, totalHeight = 0, totalWidth = 0;

        foreach (var child in Children)
        {
            child.Measure(new Size(maxWidth, double.PositiveInfinity));
            var ds = child.DesiredSize;
            if (rowX + ds.Width > maxWidth && rowX > 0)
            {
                totalHeight += rowHeight + vs;
                totalWidth = Math.Max(totalWidth, rowX - hs);
                rowX = 0;
                rowHeight = 0;
            }
            rowX += ds.Width + hs;
            rowHeight = Math.Max(rowHeight, ds.Height);
        }
        totalHeight += rowHeight;
        totalWidth = Math.Max(totalWidth, rowX - hs);
        return new Size(Math.Max(0, totalWidth), totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var maxWidth = finalSize.Width;
        var hs = HorizontalSpacing;
        var vs = VerticalSpacing;
        double rowX = 0, rowY = 0, rowHeight = 0;

        foreach (var child in Children)
        {
            var ds = child.DesiredSize;
            if (rowX + ds.Width > maxWidth && rowX > 0)
            {
                rowY += rowHeight + vs;
                rowX = 0;
                rowHeight = 0;
            }
            child.Arrange(new Rect(rowX, rowY, ds.Width, ds.Height));
            rowX += ds.Width + hs;
            rowHeight = Math.Max(rowHeight, ds.Height);
        }
        return finalSize;
    }
}
