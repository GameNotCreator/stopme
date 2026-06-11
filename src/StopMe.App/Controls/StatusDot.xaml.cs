using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace StopMe.App.Controls;

public sealed partial class StatusDot : UserControl
{
    public static readonly DependencyProperty ToneProperty =
        DependencyProperty.Register(nameof(Tone), typeof(string), typeof(StatusDot),
            new PropertyMetadata("success", OnToneChanged));

    public static readonly DependencyProperty PulseProperty =
        DependencyProperty.Register(nameof(Pulse), typeof(bool), typeof(StatusDot),
            new PropertyMetadata(false, OnPulseChanged));

    private Storyboard? _pulseStoryboard;

    public StatusDot()
    {
        InitializeComponent();
        ApplyTone();
    }

    public string Tone
    {
        get => (string)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public bool Pulse
    {
        get => (bool)GetValue(PulseProperty);
        set => SetValue(PulseProperty, value);
    }

    private static void OnToneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((StatusDot)d).ApplyTone();

    private static void OnPulseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((StatusDot)d).ApplyPulse();

    private void ApplyTone()
    {
        var brushKey = Tone?.ToLowerInvariant() switch
        {
            "success" => "StopMeSuccessBrush",
            "warning" => "StopMeWarningBrush",
            "error" => "StopMeErrorBrush",
            "accent" => "StopMeAccentBrush",
            "neutral" => "StopMeTextTertiaryBrush",
            _ => "StopMeSuccessBrush",
        };
        if (Application.Current.Resources[brushKey] is Brush b) Dot.Fill = b;
    }

    private void ApplyPulse()
    {
        _pulseStoryboard?.Stop();
        _pulseStoryboard = null;
        if (!Pulse) { Dot.Opacity = 1; return; }

        var anim = new DoubleAnimation
        {
            From = 1.0,
            To = 0.35,
            Duration = new Duration(System.TimeSpan.FromMilliseconds(800)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(anim, Dot);
        Storyboard.SetTargetProperty(anim, "Opacity");
        _pulseStoryboard = new Storyboard();
        _pulseStoryboard.Children.Add(anim);
        _pulseStoryboard.Begin();
    }
}
