using AppHider.Models;

namespace AppHider.Services;

public interface IRDSessionService
{
    /// <summary>
    /// Gets or sets safe mode flag for testing without affecting real sessions
    /// </summary>
    bool IsSafeMode { get; set; }
    
    Task<List<WTSSessionInfo>> EnumerateSessionsAsync();
    Task<bool> LogoffSessionAsync(int sessionId, int maxRetries = 3);
    Task<bool> DisconnectSessionAsync(int sessionId, int maxRetries = 3);
    Task<SessionInfo?> GetSessionInfoAsync(int sessionId);
    bool IsRemoteSession(WTSSessionInfo session);
    
    // Enhanced error handling methods
    Task<List<WTSSessionInfo>> EnumerateSessionsWithFallbackAsync();
}