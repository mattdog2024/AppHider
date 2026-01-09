using System.Collections.Concurrent;
using AppHider.Models;
using AppHider.Utils;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Services;

/// <summary>
/// Advanced caching service for remote desktop connections
/// Implements intelligent caching with TTL, batch operations, and performance optimization
/// Requirements: 8.1 (fast operations), 8.2 (quick detection), 8.5 (minimal performance impact)
/// </summary>
public class ConnectionCacheService : IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry<List<RDPConnection>>> _connectionCache;
    private readonly ConcurrentDictionary<int, CacheEntry<SessionInfo>> _sessionInfoCache;
    private readonly ConcurrentDictionary<int, CacheEntry<ProcessInfo>> _processInfoCache;
    private readonly Timer _cleanupTimer;
    private readonly SemaphoreSlim _operationSemaphore;
    
    // Cache configuration
    private readonly TimeSpan _connectionCacheTTL = TimeSpan.FromSeconds(3); // Fast refresh for connections
    private readonly TimeSpan _sessionInfoCacheTTL = TimeSpan.FromSeconds(10); // Slower refresh for session details
    private readonly TimeSpan _processInfoCacheTTL = TimeSpan.FromSeconds(5); // Medium refresh for process info
    
    // Performance tracking
    private int _cacheHits;
    private int _cacheMisses;
    private readonly object _statsLock = new object();

    public ConnectionCacheService()
    {
        _connectionCache = new ConcurrentDictionary<string, CacheEntry<List<RDPConnection>>>();
        _sessionInfoCache = new ConcurrentDictionary<int, CacheEntry<SessionInfo>>();
        _processInfoCache = new ConcurrentDictionary<int, CacheEntry<ProcessInfo>>();
        _operationSemaphore = new SemaphoreSlim(10, 10); // Allow up to 10 concurrent operations
        
        // Start cleanup timer (every 30 seconds)
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        
        FL.Log("ConnectionCacheService: Initialized with intelligent caching and performance optimization");
    }

    /// <summary>
    /// Gets cached connections or executes the factory function if cache miss
    /// Requirement 8.2: Ensure detection completes within 2 seconds through caching
    /// </summary>
    public async Task<List<RDPConnection>> GetOrSetConnectionsAsync(string cacheKey, Func<Task<List<RDPConnection>>> factory)
    {
        // Check cache first
        if (_connectionCache.TryGetValue(cacheKey, out var cacheEntry) && !cacheEntry.IsExpired)
        {
            RecordCacheHit();
            FL.Log($"ConnectionCacheService: Cache hit for {cacheKey} - {cacheEntry.Data.Count} connections");
            return cacheEntry.Data;
        }

        await _operationSemaphore.WaitAsync();
        try
        {
            // Double-check after acquiring semaphore
            if (_connectionCache.TryGetValue(cacheKey, out cacheEntry) && !cacheEntry.IsExpired)
            {
                RecordCacheHit();
                return cacheEntry.Data;
            }

            RecordCacheMiss();
            FL.Log($"ConnectionCacheService: Cache miss for {cacheKey}, executing factory function");
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var connections = await factory();
            stopwatch.Stop();
            
            // Cache the result
            var newEntry = new CacheEntry<List<RDPConnection>>(connections, _connectionCacheTTL);
            _connectionCache.AddOrUpdate(cacheKey, newEntry, (key, oldValue) => newEntry);
            
            FL.Log($"ConnectionCacheService: Cached {connections.Count} connections for {cacheKey} (took {stopwatch.ElapsedMilliseconds}ms)");
            return connections;
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets cached session info or executes the factory function if cache miss
    /// </summary>
    public async Task<SessionInfo?> GetOrSetSessionInfoAsync(int sessionId, Func<Task<SessionInfo?>> factory)
    {
        // Check cache first
        if (_sessionInfoCache.TryGetValue(sessionId, out var cacheEntry) && !cacheEntry.IsExpired)
        {
            RecordCacheHit();
            return cacheEntry.Data;
        }

        await _operationSemaphore.WaitAsync();
        try
        {
            // Double-check after acquiring semaphore
            if (_sessionInfoCache.TryGetValue(sessionId, out cacheEntry) && !cacheEntry.IsExpired)
            {
                RecordCacheHit();
                return cacheEntry.Data;
            }

            RecordCacheMiss();
            var sessionInfo = await factory();
            
            if (sessionInfo != null)
            {
                var newEntry = new CacheEntry<SessionInfo>(sessionInfo, _sessionInfoCacheTTL);
                _sessionInfoCache.AddOrUpdate(sessionId, newEntry, (key, oldValue) => newEntry);
            }
            
            return sessionInfo;
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    /// <summary>
    /// Gets cached process info or executes the factory function if cache miss
    /// </summary>
    public async Task<ProcessInfo?> GetOrSetProcessInfoAsync(int processId, Func<Task<ProcessInfo?>> factory)
    {
        // Check cache first
        if (_processInfoCache.TryGetValue(processId, out var cacheEntry) && !cacheEntry.IsExpired)
        {
            RecordCacheHit();
            return cacheEntry.Data;
        }

        await _operationSemaphore.WaitAsync();
        try
        {
            // Double-check after acquiring semaphore
            if (_processInfoCache.TryGetValue(processId, out cacheEntry) && !cacheEntry.IsExpired)
            {
                RecordCacheHit();
                return cacheEntry.Data;
            }

            RecordCacheMiss();
            var processInfo = await factory();
            
            if (processInfo != null)
            {
                var newEntry = new CacheEntry<ProcessInfo>(processInfo, _processInfoCacheTTL);
                _processInfoCache.AddOrUpdate(processId, newEntry, (key, oldValue) => newEntry);
            }
            
            return processInfo;
        }
        finally
        {
            _operationSemaphore.Release();
        }
    }

    /// <summary>
    /// Batch operation to get multiple session infos efficiently
    /// Reduces API calls and improves performance for multiple session queries
    /// </summary>
    public async Task<Dictionary<int, SessionInfo?>> GetOrSetMultipleSessionInfosAsync(
        IEnumerable<int> sessionIds, 
        Func<IEnumerable<int>, Task<Dictionary<int, SessionInfo?>>> batchFactory)
    {
        var sessionIdList = sessionIds.ToList();
        var result = new Dictionary<int, SessionInfo?>();
        var uncachedIds = new List<int>();

        // Check cache for each session ID
        foreach (var sessionId in sessionIdList)
        {
            if (_sessionInfoCache.TryGetValue(sessionId, out var cacheEntry) && !cacheEntry.IsExpired)
            {
                result[sessionId] = cacheEntry.Data;
                RecordCacheHit();
            }
            else
            {
                uncachedIds.Add(sessionId);
                RecordCacheMiss();
            }
        }

        // Fetch uncached items in batch
        if (uncachedIds.Count > 0)
        {
            await _operationSemaphore.WaitAsync();
            try
            {
                FL.Log($"ConnectionCacheService: Batch fetching {uncachedIds.Count} uncached session infos");
                
                var batchResults = await batchFactory(uncachedIds);
                
                // Cache the batch results
                foreach (var kvp in batchResults)
                {
                    result[kvp.Key] = kvp.Value;
                    
                    if (kvp.Value != null)
                    {
                        var newEntry = new CacheEntry<SessionInfo>(kvp.Value, _sessionInfoCacheTTL);
                        _sessionInfoCache.AddOrUpdate(kvp.Key, newEntry, (key, oldValue) => newEntry);
                    }
                }
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }

        return result;
    }

    /// <summary>
    /// Invalidates cache entries for specific keys or patterns
    /// </summary>
    public void InvalidateCache(string? connectionCacheKey = null, int? sessionId = null, int? processId = null)
    {
        if (connectionCacheKey != null)
        {
            _connectionCache.TryRemove(connectionCacheKey, out _);
            FL.Log($"ConnectionCacheService: Invalidated connection cache for {connectionCacheKey}");
        }

        if (sessionId.HasValue)
        {
            _sessionInfoCache.TryRemove(sessionId.Value, out _);
            FL.Log($"ConnectionCacheService: Invalidated session info cache for session {sessionId.Value}");
        }

        if (processId.HasValue)
        {
            _processInfoCache.TryRemove(processId.Value, out _);
            FL.Log($"ConnectionCacheService: Invalidated process info cache for process {processId.Value}");
        }
    }

    /// <summary>
    /// Clears all caches
    /// </summary>
    public void ClearAllCaches()
    {
        _connectionCache.Clear();
        _sessionInfoCache.Clear();
        _processInfoCache.Clear();
        
        lock (_statsLock)
        {
            _cacheHits = 0;
            _cacheMisses = 0;
        }
        
        FL.Log("ConnectionCacheService: All caches cleared");
    }

    /// <summary>
    /// Gets cache statistics for performance monitoring
    /// </summary>
    public CacheStatistics GetCacheStatistics()
    {
        lock (_statsLock)
        {
            var totalRequests = _cacheHits + _cacheMisses;
            var hitRatio = totalRequests > 0 ? (double)_cacheHits / totalRequests : 0.0;
            
            return new CacheStatistics
            {
                ConnectionCacheSize = _connectionCache.Count,
                SessionInfoCacheSize = _sessionInfoCache.Count,
                ProcessInfoCacheSize = _processInfoCache.Count,
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                HitRatio = hitRatio
            };
        }
    }

    /// <summary>
    /// Preloads cache with commonly accessed data to improve performance
    /// Requirement 8.5: Minimize performance impact by preloading
    /// </summary>
    public async Task PreloadCacheAsync(Func<Task<List<RDPConnection>>> connectionFactory)
    {
        try
        {
            FL.Log("ConnectionCacheService: Starting cache preload");
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var connections = await connectionFactory();
            stopwatch.Stop();
            
            // Cache the preloaded connections
            var cacheEntry = new CacheEntry<List<RDPConnection>>(connections, _connectionCacheTTL);
            _connectionCache.AddOrUpdate("preloaded_connections", cacheEntry, (key, oldValue) => cacheEntry);
            
            FL.Log($"ConnectionCacheService: Preloaded {connections.Count} connections in {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("PreloadCache", ex, "Failed to preload cache");
        }
    }

    /// <summary>
    /// Records a cache hit for statistics
    /// </summary>
    private void RecordCacheHit()
    {
        lock (_statsLock)
        {
            _cacheHits++;
        }
    }

    /// <summary>
    /// Records a cache miss for statistics
    /// </summary>
    private void RecordCacheMiss()
    {
        lock (_statsLock)
        {
            _cacheMisses++;
        }
    }

    /// <summary>
    /// Cleanup expired cache entries periodically
    /// </summary>
    private void CleanupExpiredEntries(object? state)
    {
        try
        {
            var cleanupCount = 0;
            
            // Cleanup connection cache
            var expiredConnectionKeys = _connectionCache
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in expiredConnectionKeys)
            {
                if (_connectionCache.TryRemove(key, out _))
                {
                    cleanupCount++;
                }
            }
            
            // Cleanup session info cache
            var expiredSessionKeys = _sessionInfoCache
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in expiredSessionKeys)
            {
                if (_sessionInfoCache.TryRemove(key, out _))
                {
                    cleanupCount++;
                }
            }
            
            // Cleanup process info cache
            var expiredProcessKeys = _processInfoCache
                .Where(kvp => kvp.Value.IsExpired)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in expiredProcessKeys)
            {
                if (_processInfoCache.TryRemove(key, out _))
                {
                    cleanupCount++;
                }
            }
            
            if (cleanupCount > 0)
            {
                FL.Log($"ConnectionCacheService: Cleaned up {cleanupCount} expired cache entries");
            }
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("CleanupExpiredEntries", ex, "Failed to cleanup expired cache entries");
        }
    }

    public void Dispose()
    {
        try
        {
            _cleanupTimer?.Dispose();
            _operationSemaphore?.Dispose();
            ClearAllCaches();
            FL.Log("ConnectionCacheService: Disposed successfully");
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("ConnectionCacheServiceDispose", ex, "Error during cache service disposal");
        }
    }
}

/// <summary>
/// Enhanced cache entry with expiration and metadata
/// </summary>
internal class CacheEntry<T>
{
    public T Data { get; set; }
    public DateTime ExpiryTime { get; set; }
    public DateTime CreatedTime { get; set; }
    public int AccessCount { get; set; }
    
    public CacheEntry(T data, TimeSpan expiry)
    {
        Data = data;
        CreatedTime = DateTime.Now;
        ExpiryTime = CreatedTime.Add(expiry);
        AccessCount = 0;
    }
    
    public bool IsExpired => DateTime.Now > ExpiryTime;
    
    public void RecordAccess()
    {
        AccessCount++;
    }
}

/// <summary>
/// Cache statistics for performance monitoring
/// </summary>
public class CacheStatistics
{
    public int ConnectionCacheSize { get; set; }
    public int SessionInfoCacheSize { get; set; }
    public int ProcessInfoCacheSize { get; set; }
    public int CacheHits { get; set; }
    public int CacheMisses { get; set; }
    public double HitRatio { get; set; }
}