namespace HostStation.App.ViewModels;

public sealed class SessionItemViewModel : ViewModelBase
{
    private string _name = "";
    private bool _isRunning;
    private long _pollCount;
    private long _errorCount;
    private string _status = "Stopped";

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
                Status = value ? "Running" : "Stopped";
        }
    }

    public long PollCount
    {
        get => _pollCount;
        set => SetProperty(ref _pollCount, value);
    }

    public long ErrorCount
    {
        get => _errorCount;
        set => SetProperty(ref _errorCount, value);
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }
}
