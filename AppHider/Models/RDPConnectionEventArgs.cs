namespace AppHider.Models;

public class RDPConnectionEventArgs : EventArgs
{
    public RDPConnection Connection { get; }
    public DateTime Timestamp { get; }
    public string? Message { get; }

    public RDPConnectionEventArgs(RDPConnection connection, string? message = null)
    {
        Connection = connection;
        Timestamp = DateTime.Now;
        Message = message;
    }
}