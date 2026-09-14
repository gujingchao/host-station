namespace HostStation.App.ViewModels;

public sealed class TagValueViewModel : ViewModelBase
{
    private string _device = "";
    private string _tag = "";
    private double _value;
    private string _quality = "Unknown";
    private DateTimeOffset _timestamp;
    private double? _hiLimit;
    private double? _loLimit;

    public string Device
    {
        get => _device;
        set => SetProperty(ref _device, value);
    }

    public string Tag
    {
        get => _tag;
        set => SetProperty(ref _tag, value);
    }

    public string Key => $"{Device}/{Tag}";

    public double Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public string Quality
    {
        get => _quality;
        set => SetProperty(ref _quality, value);
    }

    public DateTimeOffset Timestamp
    {
        get => _timestamp;
        set => SetProperty(ref _timestamp, value);
    }

    public double? HiLimit
    {
        get => _hiLimit;
        set => SetProperty(ref _hiLimit, value);
    }

    public double? LoLimit
    {
        get => _loLimit;
        set => SetProperty(ref _loLimit, value);
    }

    public string DisplayValue => Value.ToString("F2");
}
