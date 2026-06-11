using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StopMe.App.Core.ViewModels;

namespace StopMe.App.Pages;

public sealed partial class BlocklistsPage : Page
{
    public BlocklistsViewModel ViewModel { get; }

    public BlocklistsPage(BlocklistsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.RefreshAsync();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) =>
        await ViewModel.RefreshCommand.ExecuteAsync(null);

    private async void OnCreateClick(object sender, RoutedEventArgs e) =>
        await ViewModel.CreateCommand.ExecuteAsync(null);

    private async void OnDeleteClick(object sender, RoutedEventArgs e) =>
        await ViewModel.DeleteSelectedCommand.ExecuteAsync(null);
}
