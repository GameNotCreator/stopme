using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StopMe.App.Core.ViewModels;

namespace StopMe.App.Pages;

public sealed partial class TemplatesPage : Page
{
    public TemplatesViewModel ViewModel { get; }

    public TemplatesPage(TemplatesViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.RefreshAsync();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e) =>
        await ViewModel.RefreshCommand.ExecuteAsync(null);

    private async void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string key })
        {
            var template = ViewModel.Templates.FirstOrDefault(t => t.Key == key);
            await ViewModel.ApplyCommand.ExecuteAsync(template);
        }
    }

    private void OnClearScheduleClick(object sender, RoutedEventArgs e) =>
        ViewModel.ClearScheduleCommand.Execute(null);
}
