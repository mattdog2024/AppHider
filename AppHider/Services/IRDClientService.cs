using AppHider.Models;

namespace AppHider.Services;

public interface IRDClientService
{
    /// <summary>
    /// Gets or sets safe mode flag for testing without affecting real processes
    /// </summary>
    bool IsSafeMode { get; set; }
    
    Task<List<ProcessInfo>> GetMSTSCProcessesAsync();
    Task<bool> TerminateProcessAsync(int processId, int maxRetries = 2);
    Task<bool> TerminateAllMSTSCProcessesAsync();
    Task<ProcessInfo?> GetProcessInfoAsync(int processId);
    
    // Enhanced error handling methods
    Task<List<ProcessInfo>> GetMSTSCProcessesWithFallbackAsync();
    Task<(bool AllSuccessful, int SuccessCount, int TotalCount, List<string> Errors)> TerminateAllMSTSCProcessesWithDetailsAsync();
}