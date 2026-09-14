using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;

namespace HostStation.App.ViewModels;

/// <summary>Ring-buffer of samples for a single tag, exposed as Polyline points.</summary>
public sealed class TrendSeriesViewModel : ViewModelBase
{
    private readonly Queue<(DateTimeOffset Ts, double Value)> _buffer = new();
    private readonly int _capacity;
    private PointCollection _points = new();
    private string _tagKey = "";
    private double _minY;
    private double _maxY = 1;
    private double _plotWidth = 800;
    private double _plotHeight = 320;

    public TrendSeriesViewModel(int capacity = 200)
    {
        _capacity = capacity;
    }

    public string TagKey
    {
        get => _tagKey;
        set => SetProperty(ref _tagKey, value);
    }

    public PointCollection Points
    {
        get => _points;
        private set => SetProperty(ref _points, value);
    }

    public double MinY
    {
        get => _minY;
        private set => SetProperty(ref _minY, value);
    }

    public double MaxY
    {
        get => _maxY;
        private set => SetProperty(ref _maxY, value);
    }

    public ObservableCollection<string> LegendHints { get; } = new();

    public void SetPlotSize(double width, double height)
    {
        if (width <= 0 || height <= 0) return;
        _plotWidth = width;
        _plotHeight = height;
        RebuildPoints();
    }

    public void Clear()
    {
        _buffer.Clear();
        Points = new PointCollection();
        MinY = 0;
        MaxY = 1;
    }

    public void Append(DateTimeOffset ts, double value)
    {
        _buffer.Enqueue((ts, value));
        while (_buffer.Count > _capacity)
            _buffer.Dequeue();
        RebuildPoints();
    }

    private void RebuildPoints()
    {
        if (_buffer.Count == 0)
        {
            Points = new PointCollection();
            return;
        }

        var values = _buffer.Select(b => b.Value).ToList();
        var min = values.Min();
        var max = values.Max();
        if (Math.Abs(max - min) < 1e-9)
        {
            min -= 1;
            max += 1;
        }

        // pad 5%
        var pad = (max - min) * 0.05;
        min -= pad;
        max += pad;
        MinY = min;
        MaxY = max;

        var n = _buffer.Count;
        var pts = new PointCollection(n);
        var i = 0;
        foreach (var (_, v) in _buffer)
        {
            var x = n == 1 ? _plotWidth / 2 : i * (_plotWidth - 1) / (n - 1);
            var yNorm = (v - min) / (max - min);
            var y = _plotHeight - yNorm * (_plotHeight - 1);
            pts.Add(new Point(x, y));
            i++;
        }

        Points = pts;
        LegendHints.Clear();
        LegendHints.Add($"min={min:F1}");
        LegendHints.Add($"max={max:F1}");
        LegendHints.Add($"n={n}");
    }
}
