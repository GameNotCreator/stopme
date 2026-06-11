using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace StopMe.App.Controls;

public sealed partial class MiniToast : UserControl
{
    private readonly DispatcherQueueTimer _timer;

    public MiniToast()
    {
        InitializeComponent();
        _timer = DispatcherQueue.CreateTimer();
        _timer.Tick += (_, _) => { Visibility = Visibility.Collapsed; _timer.Stop(); };
    }

    public void Show(string message, TimeSpan? duration = null)
    {
        MessageText.Text = message;
        Visibility = Visibility.Visible;
        _timer.Stop();
        _timer.Interval = duration ?? TimeSpan.FromMilliseconds(2800);
        _timer.Start();
    }
}
