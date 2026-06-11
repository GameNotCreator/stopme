using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace StopMe.App.Controls;

public sealed partial class StrictToggle : UserControl
{
    public static readonly DependencyProperty IsStrictProperty =
        DependencyProperty.Register(nameof(IsStrict), typeof(bool), typeof(StrictToggle),
            new PropertyMetadata(false, OnIsStrictChanged));

    public event EventHandler<bool>? Toggled;

    public StrictToggle()
    {
        InitializeComponent();
        ApplyStyle();
    }

    public bool IsStrict
    {
        get => (bool)GetValue(IsStrictProperty);
        set => SetValue(IsStrictProperty, value);
    }

    private static void OnIsStrictChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var t = (StrictToggle)d;
        t.Switch.IsOn = (bool)e.NewValue;
        t.ApplyStyle();
        if (e.OldValue is bool o && e.NewValue is bool n && o != n) t.Toggled?.Invoke(t, n);
    }

    private void OnToggled(object sender, RoutedEventArgs e)
    {
        if (IsStrict != Switch.IsOn) IsStrict = Switch.IsOn;
    }

    private void ApplyStyle()
    {
        if (IsStrict)
        {
            LockIcon.Foreground = (Brush)Application.Current.Resources["StopMeTextPrimaryBrush"];
            Label.Foreground = (Brush)Application.Current.Resources["StopMeTextPrimaryBrush"];
        }
        else
        {
            LockIcon.Foreground = (Brush)Application.Current.Resources["StopMeTextTertiaryBrush"];
            Label.Foreground = (Brush)Application.Current.Resources["StopMeTextSecondaryBrush"];
        }
    }
}
