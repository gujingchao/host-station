using System.Collections.ObjectModel;
using System.Windows.Threading;
using HostStation.Core.Abstractions;
using HostStation.Core.Acquisition;
using HostStation.Core.Buffering;
using HostStation.Protocols.Modbus;

namespace HostStation.App.ViewModels;

public sealed class MainViewModel : ViewModelBase, IAsyncDisposable
{
    private const int TrendCapacity = 240;
    private const int MaxAlarms = 200;

    private readonly DispatcherTimer _drainTimer;
    private readonly Dictionary<string, TagValueViewModel> _tagIndex = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _hiActive = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _loActive = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _badActive = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Queue<(DateTimeOffset Ts, double Value)>> _history = new(StringComparer.Ordinal);
    private readonly Random _rng = new(42);

    private BoundedSampleQueue? _queue;
    private AcquisitionHub? _hub;
    private bool _isRunning;
    private string _statusText = "就绪 / Ready — 点击「启动采集」演示 Modbus 仿真";
    private string? _selectedTrendTag;
    private long _samplesReceived;
    private long _queueDropped;

    public MainViewModel()
    {
        Sessions = new ObservableCollection<SessionItemViewModel>();
        Tags = new ObservableCollection<TagValueViewModel>();
        Alarms = new ObservableCollection<AlarmItemViewModel>();
        TrendTagKeys = new ObservableCollection<string>();
        Trend = new TrendSeriesViewModel(TrendCapacity);

        StartCommand = new RelayCommand(StartAcquisition, () => !IsRunning);
        StopCommand = new RelayCommand(async () => await StopAcquisitionAsync(), () => IsRunning);
        ClearAlarmsCommand = new RelayCommand(() =>
        {
            Alarms.Clear();
            _hiActive.Clear();
            _loActive.Clear();
            _badActive.Clear();
        });

        _drainTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50),
        };
        _drainTimer.Tick += (_, _) => DrainQueue();
    }

    public ObservableCollection<SessionItemViewModel> Sessions { get; }
    public ObservableCollection<TagValueViewModel> Tags { get; }
    public ObservableCollection<AlarmItemViewModel> Alarms { get; }
    public ObservableCollection<string> TrendTagKeys { get; }
    public TrendSeriesViewModel Trend { get; }

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand ClearAlarmsCommand { get; }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                StartCommand.RaiseCanExecuteChanged();
                StopCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(IsStopped));
            }
        }
    }

    public bool IsStopped => !IsRunning;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string? SelectedTrendTag
    {
        get => _selectedTrendTag;
        set
        {
            if (SetProperty(ref _selectedTrendTag, value))
            {
                Trend.TagKey = value ?? "";
                RebuildSelectedTrend();
            }
        }
    }

    public long SamplesReceived
    {
        get => _samplesReceived;
        private set => SetProperty(ref _samplesReceived, value);
    }

    public long QueueDropped
    {
        get => _queueDropped;
        private set => SetProperty(ref _queueDropped, value);
    }

    private void StartAcquisition()
    {
        if (IsRunning) return;

        _queue = new BoundedSampleQueue(4096, OverflowPolicy.DropOldest);
        _hub = new AcquisitionHub(_queue);

        // Demo devices — ModbusAdapter synthetic registers (no hardware).
        var t0 = DateTimeOffset.UtcNow;
        _hub.Add(
            new ModbusAdapter("Line-A (TCP)", ModbusMode.Tcp, () => DemoRegisters(t0, deviceSeed: 1)),
            TimeSpan.FromMilliseconds(200));
        _hub.Add(
            new ModbusAdapter("Line-B (RTU)", ModbusMode.Rtu, () => DemoRegisters(t0, deviceSeed: 2)),
            TimeSpan.FromMilliseconds(250));

        // Occasional Bad quality for alarm demo (wrapper keeps Protocols untouched).
        _hub.Add(
            new QualityJitterAdapter(
                new ModbusAdapter("Diag (TCP)", ModbusMode.Tcp, () => new Dictionary<string, double>
                {
                    ["HR40100"] = 40 + 50 * Math.Sin((DateTimeOffset.UtcNow - t0).TotalSeconds * 0.7),
                }),
                () => _rng.NextDouble() < 0.08),
            TimeSpan.FromMilliseconds(400));

        Sessions.Clear();
        foreach (var session in _hub.Sessions)
        {
            Sessions.Add(new SessionItemViewModel
            {
                Name = session.Name,
                IsRunning = false,
                PollCount = 0,
                ErrorCount = 0,
            });
        }

        // Default hi/lo limits for known demo tags (prefixed for device attribution)
        EnsureTag("Line-A (TCP)", "A.HR40001", hi: 80, lo: 20);
        EnsureTag("Line-A (TCP)", "A.HR40002", hi: 90, lo: 5);
        EnsureTag("Line-B (RTU)", "B.HR40001", hi: 75, lo: 15);
        EnsureTag("Line-B (RTU)", "B.HR40002", hi: 85, lo: 10);
        EnsureTag("Diag (TCP)", "D.HR40100", hi: 85, lo: 10);

        _hub.StartAll();
        IsRunning = true;
        _drainTimer.Start();
        StatusText = "采集运行中 / Acquiring — Modbus 仿真数据写入 BoundedSampleQueue";
        RefreshSessionStats();
    }

    private async Task StopAcquisitionAsync()
    {
        _drainTimer.Stop();
        if (_hub is not null)
        {
            await _hub.StopAllAsync().ConfigureAwait(true);
            await _hub.DisposeAsync().ConfigureAwait(true);
            _hub = null;
        }

        _queue = null;
        IsRunning = false;
        RefreshSessionStats();
        StatusText = "已停止 / Stopped";
    }

    private void DrainQueue()
    {
        if (_queue is null) return;

        var drained = 0;
        while (drained < 512 && _queue.TryRead(out var sample))
        {
            ApplySample(sample);
            drained++;
            SamplesReceived++;
        }

        QueueDropped = _queue.Dropped;
        RefreshSessionStats();
    }

    private void ApplySample(TagSample sample)
    {
        // TagSample has no device id — demo tags are prefixed (A./B./D.) for attribution.
        var device = GuessDevice(sample.Tag);
        var key = $"{device}/{sample.Tag}";

        if (!_tagIndex.TryGetValue(key, out var vm))
        {
            vm = EnsureTag(device, sample.Tag, hi: 80, lo: 10);
        }

        vm.Value = sample.Value;
        vm.Quality = sample.Quality ?? "Good";
        vm.Timestamp = sample.Timestamp;
        RaisePropertyChanged(nameof(Tags)); // collection items notify themselves

        if (!_history.TryGetValue(key, out var hist))
        {
            hist = new Queue<(DateTimeOffset, double)>();
            _history[key] = hist;
        }

        hist.Enqueue((sample.Timestamp, sample.Value));
        while (hist.Count > TrendCapacity)
            hist.Dequeue();

        if (!TrendTagKeys.Contains(key))
            TrendTagKeys.Add(key);

        // First selection rebuilds from history (already includes this sample).
        if (SelectedTrendTag is null)
            SelectedTrendTag = key;
        else if (SelectedTrendTag == key)
            Trend.Append(sample.Timestamp, sample.Value);

        EvaluateAlarms(vm);
    }

    private static string GuessDevice(string tag)
    {
        // Unique demo tags: A.* / B.* / Diag.*
        if (tag.StartsWith("A.", StringComparison.Ordinal)) return "Line-A (TCP)";
        if (tag.StartsWith("B.", StringComparison.Ordinal)) return "Line-B (RTU)";
        if (tag.StartsWith("D.", StringComparison.Ordinal)) return "Diag (TCP)";
        return "Unknown";
    }

    private TagValueViewModel EnsureTag(string device, string tag, double? hi, double? lo)
    {
        var key = $"{device}/{tag}";
        if (_tagIndex.TryGetValue(key, out var existing))
        {
            if (hi.HasValue) existing.HiLimit = hi;
            if (lo.HasValue) existing.LoLimit = lo;
            return existing;
        }

        var vm = new TagValueViewModel
        {
            Device = device,
            Tag = tag,
            HiLimit = hi,
            LoLimit = lo,
            Quality = "—",
            Timestamp = DateTimeOffset.Now,
        };
        _tagIndex[key] = vm;
        Tags.Add(vm);
        return vm;
    }

    private void EvaluateAlarms(TagValueViewModel tag)
    {
        var key = tag.Key;
        var now = tag.Timestamp;

        // Bad quality
        var isBad = !string.Equals(tag.Quality, "Good", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(tag.Quality)
                    && tag.Quality != "—";
        RaiseOrClear(key, "bad", isBad, _badActive,
            () => new AlarmItemViewModel(tag.Device, tag.Tag, $"质量异常 Quality={tag.Quality}", "Bad", now));

        if (tag.HiLimit is double hi && tag.Value > hi)
        {
            RaiseOrClear(key, "hi", true, _hiActive,
                () => new AlarmItemViewModel(tag.Device, tag.Tag, $"超上限 Hi>{hi:F1} (值={tag.Value:F2})", "Hi", now));
        }
        else
        {
            RaiseOrClear(key, "hi", false, _hiActive, null);
        }

        if (tag.LoLimit is double lo && tag.Value < lo)
        {
            RaiseOrClear(key, "lo", true, _loActive,
                () => new AlarmItemViewModel(tag.Device, tag.Tag, $"超下限 Lo<{lo:F1} (值={tag.Value:F2})", "Lo", now));
        }
        else
        {
            RaiseOrClear(key, "lo", false, _loActive, null);
        }
    }

    private void RaiseOrClear(
        string tagKey,
        string kind,
        bool active,
        Dictionary<string, bool> map,
        Func<AlarmItemViewModel>? create)
    {
        var mapKey = $"{tagKey}:{kind}";
        map.TryGetValue(mapKey, out var wasActive);
        if (active && !wasActive && create is not null)
        {
            map[mapKey] = true;
            Alarms.Insert(0, create());
            while (Alarms.Count > MaxAlarms)
                Alarms.RemoveAt(Alarms.Count - 1);
        }
        else if (!active && wasActive)
        {
            map[mapKey] = false;
            // mark matching active alarms as cleared
            foreach (var a in Alarms)
            {
                if (a.IsActive && $"{a.Device}/{a.Tag}" == tagKey
                    && ((kind == "hi" && a.Severity == "Hi")
                        || (kind == "lo" && a.Severity == "Lo")
                        || (kind == "bad" && a.Severity == "Bad")))
                {
                    a.IsActive = false;
                }
            }
        }
        else if (active)
        {
            map[mapKey] = true;
        }
    }

    private void RebuildSelectedTrend()
    {
        Trend.Clear();
        if (SelectedTrendTag is null) return;
        if (!_history.TryGetValue(SelectedTrendTag, out var hist)) return;
        foreach (var (ts, v) in hist)
            Trend.Append(ts, v);
    }

    private void RefreshSessionStats()
    {
        if (_hub is null)
        {
            foreach (var s in Sessions)
            {
                s.IsRunning = false;
            }
            return;
        }

        var live = _hub.Sessions;
        for (var i = 0; i < Sessions.Count && i < live.Count; i++)
        {
            Sessions[i].Name = live[i].Name;
            Sessions[i].IsRunning = live[i].IsRunning;
            Sessions[i].PollCount = live[i].PollCount;
            Sessions[i].ErrorCount = live[i].ErrorCount;
        }
    }

    private static IReadOnlyDictionary<string, double> DemoRegisters(DateTimeOffset t0, int deviceSeed)
    {
        var t = (DateTimeOffset.UtcNow - t0).TotalSeconds;
        var prefix = deviceSeed == 1 ? "A." : "B.";
        var phase = deviceSeed * 0.9;
        return new Dictionary<string, double>
        {
            [prefix + "HR40001"] = 50 + 40 * Math.Sin(t * 0.55 + phase),
            [prefix + "HR40002"] = 30 + 25 * Math.Sin(t * 0.35 + phase * 1.3) + 5 * Math.Sin(t * 2.1),
        };
    }

    public async ValueTask DisposeAsync()
    {
        _drainTimer.Stop();
        if (_hub is not null)
            await _hub.DisposeAsync().ConfigureAwait(false);
        _hub = null;
        _queue = null;
    }
}

/// <summary>
/// Thin App-side wrapper: re-emits inner samples and occasionally marks Quality=Bad
/// so the alarm list can be demonstrated without changing HostStation.Protocols.
/// </summary>
internal sealed class QualityJitterAdapter : IProtocolAdapter
{
    private readonly IProtocolAdapter _inner;
    private readonly Func<bool> _shouldInjectBad;

    public QualityJitterAdapter(IProtocolAdapter inner, Func<bool> shouldInjectBad)
    {
        _inner = inner;
        _shouldInjectBad = shouldInjectBad;
        Name = inner.Name;
    }

    public string Name { get; }

    public async Task<IReadOnlyList<TagSample>> PollAsync(CancellationToken cancellationToken = default)
    {
        var samples = await _inner.PollAsync(cancellationToken).ConfigureAwait(false);
        if (!_shouldInjectBad())
        {
            // Remap tags with D. prefix for device attribution
            return samples
                .Select(s => s with { Tag = s.Tag.StartsWith("D.", StringComparison.Ordinal) ? s.Tag : "D." + s.Tag })
                .ToList();
        }

        return samples
            .Select(s =>
            {
                var tag = s.Tag.StartsWith("D.", StringComparison.Ordinal) ? s.Tag : "D." + s.Tag;
                return s with { Tag = tag, Quality = "Bad" };
            })
            .ToList();
    }
}
