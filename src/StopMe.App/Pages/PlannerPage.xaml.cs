using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StopMe.App.Core.ViewModels;

namespace StopMe.App.Pages;

public sealed partial class PlannerPage : Page
{
    public PlannerViewModel ViewModel { get; }

    public PlannerPage(PlannerViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.RefreshAsync();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) =>
        await ViewModel.RefreshCommand.ExecuteAsync(null);

    private async void OnCreateOnlyClick(object sender, RoutedEventArgs e) =>
        await ViewModel.CreateOnlyCommand.ExecuteAsync(null);

    private async void OnCreateAndStartClick(object sender, RoutedEventArgs e) =>
        await ViewModel.CreateAndStartCommand.ExecuteAsync(null);

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox box)
            ViewModel.Password = box.Password;
    }
}
