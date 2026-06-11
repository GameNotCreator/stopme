using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace StopMe.App.Controls;

public sealed partial class EmptyState : UserControl
{
    public static readonly DependencyProperty IconNameProperty =
        DependencyProperty.Register(nameof(IconName), typeof(string), typeof(EmptyState),
            new PropertyMetadata("info", (d, e) => ((EmptyState)d).Icon.IconName = (string)e.NewValue));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(EmptyState),
            new PropertyMetadata("", (d, e) => ((EmptyState)d).TitleText.Text = (string)e.NewValue));

    public static readonly DependencyProperty BodyProperty =
        DependencyProperty.Register(nameof(Body), typeof(string), typeof(EmptyState),
            new PropertyMetadata("", (d, e) =>
            {
                var c = (EmptyState)d;
                var v = (string)e.NewValue;
                c.BodyText.Text = v;
                c.BodyText.Visibility = string.IsNullOrEmpty(v) ? Visibility.Collapsed : Visibility.Visible;
            }));

    public static readonly DependencyProperty ActionProperty =
        DependencyProperty.Register(nameof(Action), typeof(object), typeof(EmptyState),
            new PropertyMetadata(null, (d, e) => ((EmptyState)d).ActionSlot.Content = e.NewValue));

    public EmptyState() => InitializeComponent();

    public string IconName
    {
        get => (string)GetValue(IconNameProperty);
        set => SetValue(IconNameProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Body
    {
        get => (string)GetValue(BodyProperty);
        set => SetValue(BodyProperty, value);
    }

    public object? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }
}
