using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace StopMe.App.Controls;

// Custom circular progress ring — the prototype's <ProgressRing> from
// stopme-ui.jsx. Built from two stroked ellipses (track + arc). The arc is
// drawn via stroke-dasharray on a Path so 0..1 progress maps to 0..circumference.
public sealed partial class StopMeProgressRing : UserControl
{
    public static readonly DependencyProperty RingSizeProperty =
        DependencyProperty.Register(nameof(RingSize), typeof(double), typeof(StopMeProgressRing),
            new PropertyMetadata(210.0, OnVisualChanged));

    public static readonly DependencyProperty StrokeWidthProperty =
        DependencyProperty.Register(nameof(StrokeWidth), typeof(double), typeof(StopMeProgressRing),
            new PropertyMetadata(11.0, OnVisualChanged));

    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(nameof(Progress), typeof(double), typeof(StopMeProgressRing),
            new PropertyMetadata(0.0, OnVisualChanged));

    public static readonly DependencyProperty RingBrushProperty =
        DependencyProperty.Register(nameof(RingBrush), typeof(Brush), typeof(StopMeProgressRing),
            new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(StopMeProgressRing),
            new PropertyMetadata(null, OnVisualChanged));

    public static readonly DependencyProperty CenterContentValueProperty =
        DependencyProperty.Register(nameof(CenterContentValue), typeof(object), typeof(StopMeProgressRing),
            new PropertyMetadata(null));

    public StopMeProgressRing()
    {
        InitializeComponent();
        Loaded += (_, _) => Redraw();
    }

    public double RingSize
    {
        get => (double)GetValue(RingSizeProperty);
        set => SetValue(RingSizeProperty, value);
    }

    public double StrokeWidth
    {
        get => (double)GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, Math.Clamp(value, 0.0, 1.0));
    }

    public Brush? RingBrush
    {
        get => (Brush?)GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    public Brush? TrackBrush
    {
        get => (Brush?)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public object? CenterContentValue
    {
        get => GetValue(CenterContentValueProperty);
        set => SetValue(CenterContentValueProperty, value);
    }

    private static void OnVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((StopMeProgressRing)d).Redraw();

    private void Redraw()
    {
        Canvas.Children.Clear();
        var size = RingSize;
        var stroke = StrokeWidth;
        if (size <= 0 || stroke <= 0) return;

        Root.Width = size;
        Root.Height = size;
        Canvas.Width = size;
        Canvas.Height = size;

        var radius = (size - stroke) / 2.0;
        var center = size / 2.0;
        var circumference = 2 * Math.PI * radius;

        var trackBrush = TrackBrush
            ?? (Application.Current.Resources["StopMeAccentSoft2Brush"] as Brush);
        var ringBrush = RingBrush
            ?? (Application.Current.Resources["StopMeAccentBrush"] as Brush);

        var track = new Ellipse
        {
            Width = size - stroke,
            Height = size - stroke,
            Stroke = trackBrush,
            StrokeThickness = stroke,
            Fill = null,
        };
        Canvas.SetLeft(track, stroke / 2.0);
        Canvas.SetTop(track, stroke / 2.0);
        Canvas.Children.Add(track);

        var progressPct = Math.Clamp(Progress, 0.0, 1.0);
        if (progressPct > 0.0)
        {
            var arc = new Ellipse
            {
                Width = size - stroke,
                Height = size - stroke,
                Stroke = ringBrush,
                StrokeThickness = stroke,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Fill = null,
                StrokeDashArray = new DoubleCollection { circumference / stroke, circumference / stroke },
                StrokeDashOffset = circumference * (1.0 - progressPct) / stroke,
            };
            // Start at top: rotate -90 around centre.
            arc.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            arc.RenderTransform = new RotateTransform { Angle = -90, CenterX = (size - stroke) / 2.0, CenterY = (size - stroke) / 2.0 };
            Canvas.SetLeft(arc, stroke / 2.0);
            Canvas.SetTop(arc, stroke / 2.0);
            Canvas.Children.Add(arc);
        }
    }
}
