namespace AppHider.Models;

public class EmergencyDisconnectEventArgs : EventArgs
{
    public EmergencyDisconnectResult? Result { get; }
    public DateTime Timestamp { get; }
    public string? Message { get; }
    public bool IsCompleted { get; }

    public EmergencyDisconnectEventArgs(string? message = null, EmergencyDisconnectResult? result = null, bool isCompleted = false)
    {
        Message = message;
        Result = result;
        Timestamp = DateTime.Now;
        IsCompleted = isCompleted;
    }
}