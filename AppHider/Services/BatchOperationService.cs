using System.Collections.Concurrent;
using AppHider.Models;
using AppHider.Utils;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Services;

/// <summary>
/// Batch operation service for optimizing remote desktop operations
/// Reduces API calls and improves performance through batching and parallel processing
/// Requirements: 8.1 (fast operations), 8.4 (efficient CPU usage)
/// </summary>
public class BatchOperationService : IDisposable
{
    private readonly SemaphoreSlim _batchSemaphore;
    private readonly ConcurrentQueue<BatchOperation> _operationQueue;
    private readonly Timer _batchProcessor;
    private readonly object _lockObject = new object();
    
    // Batch configuration
    private readonly int _maxBatchSize = 10;
    private readonly TimeSpan _batchInterval = TimeSpan.FromMilliseconds(100); // Process batches every 100ms
    private readonly TimeSpan _maxWaitTime = TimeSpan.FromSeconds(1); // Maximum wait before forcing batch processing
    
    // Performance tracking
    private int _totalOperations;
    private int _batchedOperations;
    private readonly List<TimeSpan> _batchProcessingTimes = new List<TimeSpan>();

    public BatchOperationService()
    {
        _batchSemaphore = new SemaphoreSlim(1, 1);
        _operationQueue = new ConcurrentQueue<BatchOperation>();
        
        // Start batch processor timer
        _batchProcessor = new Timer(ProcessBatchQueueCallback, null, _batchInterval, _batchInterval);
        
        FL.Log("BatchOperationService: Initialized with intelligent batching and parallel processing");
    }

    /// <summary>
    /// Queues a session termination operation for batch processing
    /// Requirements: 8.3 (termination within 1 second), 8.4 (efficient processing)
    /// </summary>
    public async Task<bool> QueueSessionTerminationAsync(int sessionId, bool isLogoff = true)
    {
        var operation = new BatchOperation
        {
            Type = BatchOperationType.SessionTermination,
            SessionId = sessionId,
            IsLogoff = isLogoff,
            QueuedTime = DateTime.Now,
            CompletionSource = new TaskCompletionSource<bool>()
        };

        _operationQueue.Enqueue(operation);
        Interlocked.Increment(ref _totalOperations);
        
        FL.Log($"BatchOperationService: Queued session termination for session {sessionId} (logoff: {isLogoff})");
        
        // If queue is getting full, process immediately
        if (_operationQueue.Count >= _maxBatchSize)
        {
            _ = Task.Run(async () => await ProcessBatchQueue(null));
        }
        
        return await operation.CompletionSource.Task;
    }

    /// <summary>
    /// Queues a process termination operation for batch processing
    /// </summary>
    public async Task<bool> QueueProcessTerminationAsync(int processId, string processName)
    {
        var operation = new BatchOperation
        {
            Type = BatchOperationType.ProcessTermination,
            ProcessId = processId,
            ProcessName = processName,
            QueuedTime = DateTime.Now,
            CompletionSource = new TaskCompletionSource<bool>()
        };

        _operationQueue.Enqueue(operation);
        Interlocked.Increment(ref _totalOperations);
        
        FL.Log($"BatchOperationService: Queued process termination for process {processId} ({processName})");
        
        // If queue is getting full, process immediately
        if (_operationQueue.Count >= _maxBatchSize)
        {
            _ = Task.Run(async () => await ProcessBatchQueue(null));
        }
        
        return await operation.CompletionSource.Task;
    }

    /// <summary>
    /// Executes multiple session terminations in parallel with optimized batching
    /// Requirements: 8.1 (complete within 10 seconds), 8.3 (start within 1 second)
    /// </summary>
    public async Task<BatchTerminationResult> ExecuteSessionTerminationBatchAsync(
        IEnumerable<int> sessionIds, 
        Func<int, bool, Task<bool>> terminationFunction,
        bool isLogoff = true)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var sessionIdList = sessionIds.ToList();
        var result = new BatchTerminationResult();
        
        if (sessionIdList.Count == 0)
        {
            return result;
        }

        FL.Log($"BatchOperationService: Starting batch session termination for {sessionIdList.Count} sessions");

        try
        {
            // Process in parallel batches for optimal performance
            var batchSize = Math.Min(_maxBatchSize, sessionIdList.Count);
            var batches = sessionIdList
                .Select((sessionId, index) => new { sessionId, index })
                .GroupBy(x => x.index / batchSize)
                .Select(g => g.Select(x => x.sessionId).ToList())
                .ToList();

            var allTasks = new List<Task<(int sessionId, bool success, string error)>>();

            foreach (var batch in batches)
            {
                var batchTasks = batch.Select(async sessionId =>
                {
                    try
                    {
                        var success = await terminationFunction(sessionId, isLogoff);
                        return (sessionId, success, string.Empty);
                    }
                    catch (Exception ex)
                    {
                        return (sessionId, false, ex.Message);
                    }
                });

                allTasks.AddRange(batchTasks);
            }

            // Wait for all terminations to complete
            var results = await Task.WhenAll(allTasks);
            
            // Process results
            foreach (var (sessionId, success, error) in results)
            {
                if (success)
                {
                    result.SuccessfulTerminations.Add(sessionId);
                }
                else
                {
                    result.FailedTerminations[sessionId] = error;
                }
            }

            result.TotalAttempted = sessionIdList.Count;
            result.ExecutionTime = stopwatch.Elapsed;
            result.Success = result.SuccessfulTerminations.Count == result.TotalAttempted;

            Interlocked.Add(ref _batchedOperations, sessionIdList.Count);
            
            lock (_lockObject)
            {
                _batchProcessingTimes.Add(result.ExecutionTime);
                if (_batchProcessingTimes.Count > 100)
                {
                    _batchProcessingTimes.RemoveAt(0);
                }
            }

            FL.Log($"BatchOperationService: Batch session termination completed - {result.SuccessfulTerminations.Count}/{result.TotalAttempted} successful in {result.ExecutionTime.TotalMilliseconds:F0}ms");

            return result;
        }
        catch (Exception ex)
        {
            result.ExecutionTime = stopwatch.Elapsed;
            result.Success = false;
            FL.LogDetailedError("ExecuteSessionTerminationBatch", ex, "Failed to execute session termination batch");
            return result;
        }
    }

    /// <summary>
    /// Executes multiple process terminations in parallel with optimized batching
    /// </summary>
    public async Task<BatchTerminationResult> ExecuteProcessTerminationBatchAsync(
        IEnumerable<int> processIds,
        Func<int, Task<bool>> terminationFunction)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var processIdList = processIds.ToList();
        var result = new BatchTerminationResult();
        
        if (processIdList.Count == 0)
        {
            return result;
        }

        FL.Log($"BatchOperationService: Starting batch process termination for {processIdList.Count} processes");

        try
        {
            // Process in parallel with controlled concurrency
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);
            var tasks = processIdList.Select(async processId =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var success = await terminationFunction(processId);
                    return (processId, success, string.Empty);
                }
                catch (Exception ex)
                {
                    return (processId, false, ex.Message);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            
            // Process results
            foreach (var (processId, success, error) in results)
            {
                if (success)
                {
                    result.SuccessfulTerminations.Add(processId);
                }
                else
                {
                    result.FailedTerminations[processId] = error;
                }
            }

            result.TotalAttempted = processIdList.Count;
            result.ExecutionTime = stopwatch.Elapsed;
            result.Success = result.SuccessfulTerminations.Count == result.TotalAttempted;

            Interlocked.Add(ref _batchedOperations, processIdList.Count);
            
            lock (_lockObject)
            {
                _batchProcessingTimes.Add(result.ExecutionTime);
                if (_batchProcessingTimes.Count > 100)
                {
                    _batchProcessingTimes.RemoveAt(0);
                }
            }

            FL.Log($"BatchOperationService: Batch process termination completed - {result.SuccessfulTerminations.Count}/{result.TotalAttempted} successful in {result.ExecutionTime.TotalMilliseconds:F0}ms");

            return result;
        }
        catch (Exception ex)
        {
            result.ExecutionTime = stopwatch.Elapsed;
            result.Success = false;
            FL.LogDetailedError("ExecuteProcessTerminationBatch", ex, "Failed to execute process termination batch");
            return result;
        }
    }

    /// <summary>
    /// Gets batch operation statistics for performance monitoring
    /// </summary>
    public BatchOperationStatistics GetStatistics()
    {
        lock (_lockObject)
        {
            var avgProcessingTime = _batchProcessingTimes.Count > 0 
                ? _batchProcessingTimes.Select(t => t.TotalMilliseconds).Average() 
                : 0.0;
            
            return new BatchOperationStatistics
            {
                TotalOperations = _totalOperations,
                BatchedOperations = _batchedOperations,
                QueuedOperations = _operationQueue.Count,
                AverageProcessingTime = avgProcessingTime,
                BatchingEfficiency = _totalOperations > 0 ? (double)_batchedOperations / _totalOperations : 0.0
            };
        }
    }

    /// <summary>
    /// Timer callback wrapper for ProcessBatchQueue
    /// </summary>
    private void ProcessBatchQueueCallback(object? state)
    {
        _ = Task.Run(async () => await ProcessBatchQueue(state));
    }

    /// <summary>
    /// Forces immediate processing of the batch queue
    /// </summary>
    public async Task FlushBatchQueueAsync()
    {
        await ProcessBatchQueue(null);
    }

    /// <summary>
    /// Processes the batch queue periodically or when triggered
    /// </summary>
    private async Task ProcessBatchQueue(object? state)
    {
        if (_operationQueue.IsEmpty)
        {
            return;
        }

        await _batchSemaphore.WaitAsync();
        try
        {
            var operations = new List<BatchOperation>();
            var processedCount = 0;
            
            // Dequeue operations for processing
            while (operations.Count < _maxBatchSize && _operationQueue.TryDequeue(out var operation))
            {
                // Check if operation has been waiting too long
                if (DateTime.Now - operation.QueuedTime > _maxWaitTime)
                {
                    FL.Log($"BatchOperationService: Operation {operation.Type} for ID {operation.SessionId ?? operation.ProcessId} exceeded max wait time");
                }
                
                operations.Add(operation);
            }

            if (operations.Count == 0)
            {
                return;
            }

            FL.Log($"BatchOperationService: Processing batch of {operations.Count} operations");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Group operations by type for efficient processing
            var sessionTerminations = operations.Where(o => o.Type == BatchOperationType.SessionTermination).ToList();
            var processTerminations = operations.Where(o => o.Type == BatchOperationType.ProcessTermination).ToList();

            // Process session terminations
            if (sessionTerminations.Count > 0)
            {
                await ProcessSessionTerminationBatch(sessionTerminations);
                processedCount += sessionTerminations.Count;
            }

            // Process process terminations
            if (processTerminations.Count > 0)
            {
                await ProcessProcessTerminationBatch(processTerminations);
                processedCount += processTerminations.Count;
            }

            stopwatch.Stop();
            
            lock (_lockObject)
            {
                _batchProcessingTimes.Add(stopwatch.Elapsed);
                if (_batchProcessingTimes.Count > 100)
                {
                    _batchProcessingTimes.RemoveAt(0);
                }
            }

            FL.Log($"BatchOperationService: Processed batch of {processedCount} operations in {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("ProcessBatchQueue", ex, "Failed to process batch queue");
        }
        finally
        {
            _batchSemaphore.Release();
        }
    }

    /// <summary>
    /// Processes a batch of session termination operations
    /// </summary>
    private async Task ProcessSessionTerminationBatch(List<BatchOperation> operations)
    {
        var tasks = operations.Select(async operation =>
        {
            try
            {
                // Simulate session termination (replace with actual implementation)
                await Task.Delay(50); // Simulate API call time
                var success = true; // Replace with actual termination logic
                
                operation.CompletionSource.SetResult(success);
                return success;
            }
            catch (Exception ex)
            {
                operation.CompletionSource.SetException(ex);
                return false;
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Processes a batch of process termination operations
    /// </summary>
    private async Task ProcessProcessTerminationBatch(List<BatchOperation> operations)
    {
        var tasks = operations.Select(async operation =>
        {
            try
            {
                // Simulate process termination (replace with actual implementation)
                await Task.Delay(30); // Simulate process kill time
                var success = true; // Replace with actual termination logic
                
                operation.CompletionSource.SetResult(success);
                return success;
            }
            catch (Exception ex)
            {
                operation.CompletionSource.SetException(ex);
                return false;
            }
        });

        await Task.WhenAll(tasks);
    }

    public void Dispose()
    {
        try
        {
            _batchProcessor?.Dispose();
            _batchSemaphore?.Dispose();
            
            // Complete any remaining operations
            while (_operationQueue.TryDequeue(out var operation))
            {
                operation.CompletionSource.SetCanceled();
            }
            
            FL.Log("BatchOperationService: Disposed successfully");
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("BatchOperationServiceDispose", ex, "Error during batch operation service disposal");
        }
    }
}

/// <summary>
/// Batch operation data structure
/// </summary>
internal class BatchOperation
{
    public BatchOperationType Type { get; set; }
    public int? SessionId { get; set; }
    public int? ProcessId { get; set; }
    public string? ProcessName { get; set; }
    public bool IsLogoff { get; set; }
    public DateTime QueuedTime { get; set; }
    public TaskCompletionSource<bool> CompletionSource { get; set; } = new TaskCompletionSource<bool>();
}

/// <summary>
/// Batch operation types
/// </summary>
internal enum BatchOperationType
{
    SessionTermination,
    ProcessTermination
}

/// <summary>
/// Batch termination result
/// </summary>
public class BatchTerminationResult
{
    public bool Success { get; set; }
    public int TotalAttempted { get; set; }
    public List<int> SuccessfulTerminations { get; set; } = new List<int>();
    public Dictionary<int, string> FailedTerminations { get; set; } = new Dictionary<int, string>();
    public TimeSpan ExecutionTime { get; set; }
}

/// <summary>
/// Batch operation statistics
/// </summary>
public class BatchOperationStatistics
{
    public int TotalOperations { get; set; }
    public int BatchedOperations { get; set; }
    public int QueuedOperations { get; set; }
    public double AverageProcessingTime { get; set; }
    public double BatchingEfficiency { get; set; }
}