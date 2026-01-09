namespace AppHider.Models;

public class EmergencyDisconnectResult
{
    public bool Success { get; set; }
    public int SessionsTerminated { get; set; }
    public int ClientsTerminated { get; set; }
    public bool NetworkDisconnected { get; set; }
    public List<string> Errors { get; set; } = new();
    public TimeSpan ExecutionTime { get; set; }
}