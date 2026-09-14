using System.Windows;
using System.Windows.Controls;
using HostStation.App.ViewModels;

namespace HostStation.App.Views;

public partial class TrendChartControl : UserControl
{
    public TrendChartControl()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => SyncSize();
    }

    private void PlotCanvas_OnSizeChanged(object sender, SizeChangedEventArgs e) => SyncSize();

    private void SyncSize()
    {
        if (DataContext is TrendSeriesViewModel series)
            series.SetPlotSize(PlotCanvas.ActualWidth, PlotCanvas.ActualHeight);
    }
}
