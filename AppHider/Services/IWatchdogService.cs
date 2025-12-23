namespace AppHider.Services;

public interface IWatchdogService
{
    Task StartWatchdogAsync();
    Task StopWatchdogAsync();
    bool IsWatchdogRunning { get; }
}
