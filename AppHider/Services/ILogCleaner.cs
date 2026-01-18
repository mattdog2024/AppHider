using System.Threading.Tasks;

namespace AppHider.Services;

public interface ILogCleaner
{
    /// <summary>
    /// Cleans all user activity logs (RDP, Run history, Recent files).
    /// </summary>
    Task CleanAllLogsAsync();

    /// <summary>
    /// Cleans specific Remote Desktop Connection logs.
    /// </summary>
    Task CleanRDPLogsAsync();

    /// <summary>
    /// Cleans system usage history (Run, Explorer, etc).
    /// </summary>
    Task CleanSystemLogsAsync();
}
