namespace HostStation.App.ViewModels;

public sealed class AlarmItemViewModel : ViewModelBase
{
    private bool _isActive = true;

    public AlarmItemViewModel(string device, string tag, string message, string severity, DateTimeOffset raisedAt)
    {
        Device = device;
        Tag = tag;
        Message = message;
        Severity = severity;
        RaisedAt = raisedAt;
    }

    public string Device { get; }
    public string Tag { get; }
    public string Message { get; }
    public string Severity { get; }
    public DateTimeOffset RaisedAt { get; }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public string DisplayTime => RaisedAt.ToLocalTime().ToString("HH:mm:ss.fff");
}
