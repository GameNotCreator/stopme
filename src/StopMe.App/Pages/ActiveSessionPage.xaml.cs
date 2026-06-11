using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StopMe.App.Core.ViewModels;

namespace StopMe.App.Pages;

public sealed partial class ActiveSessionPage : Page
{
    public ActiveSessionViewModel ViewModel { get; }

    public ActiveSessionPage(ActiveSessionViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.RefreshAsync();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) =>
        await ViewModel.RefreshCommand.ExecuteAsync(null);

    private async void OnStopClick(object sender, RoutedEventArgs e) =>
        await ViewModel.StopSessionCommand.ExecuteAsync(null);
}
