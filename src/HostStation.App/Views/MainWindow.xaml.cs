using System.Windows;
using HostStation.App.ViewModels;

namespace HostStation.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
    }

    private async void Window_OnClosed(object? sender, EventArgs e)
    {
        await _vm.DisposeAsync().ConfigureAwait(true);
    }
}
