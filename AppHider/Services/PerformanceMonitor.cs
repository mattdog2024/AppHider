using System.Diagnostics;
using System.Management;
using AppHider.Utils;
using FL = AppHider.Utils.FileLogger;

namespace AppHider.Services;

/// <summary>
/// Performance monitoring service for remote desktop operations
/// Tracks CPU usage, memory consumption, and operation timing
/// Requirements: 8.4 (CPU monitoring), 8.5 (performance impact)
/// </summary>
public class PerformanceMonitor : IDisposable
{
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _memoryCounter;
    private readonly Timer _monitoringTimer;
    private readonly object _lockObject = new object();
    
    // Performance metrics
    private double _averageCpuUsage;
    private long _peakMemoryUsage;
    private int _measurementCount;
    private readonly List<double> _cpuSamples = new List<double>();
    private readonly List<long> _memorySamples = new List<long>();
    
    // Operation timing
    private readonly Dictionary<string, List<TimeSpan>> _operationTimes = new Dictionary<string, List<TimeSpan>>();
    
    public event EventHandler<PerformanceMetricsEventArgs>? PerformanceAlert;

    public PerformanceMonitor()
    {
        try
        {
            // Initialize performance counters
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            
            // Start monitoring timer (every 5 seconds)
            _monitoringTimer = new Timer(CollectMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
            
            FL.Log("PerformanceMonitor: Initialized with CPU and memory monitoring");
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("PerformanceMonitorInit", ex, "Failed to initialize performance monitoring");
            throw;
        }
    }

    /// <summary>
    /// Records the execution time of an operation
    /// </summary>
    public void RecordOperationTime(string operationName, TimeSpan executionTime)
    {
        lock (_lockObject)
        {
            if (!_operationTimes.ContainsKey(operationName))
            {
                _operationTimes[operationName] = new List<TimeSpan>();
            }
            
            _operationTimes[operationName].Add(executionTime);
            
            // Keep only last 100 measurements per operation
            if (_operationTimes[operationName].Count > 100)
            {
                _operationTimes[operationName].RemoveAt(0);
            }
        }
        
        FL.Log($"PerformanceMonitor: {operationName} completed in {executionTime.TotalMilliseconds:F2}ms");
    }

    /// <summary>
    /// Gets average execution time for an operation
    /// </summary>
    public TimeSpan GetAverageOperationTime(string operationName)
    {
        lock (_lockObject)
        {
            if (!_operationTimes.ContainsKey(operationName) || _operationTimes[operationName].Count == 0)
            {
                return TimeSpan.Zero;
            }
            
            var times = _operationTimes[operationName];
            var averageTicks = times.Select(t => t.Ticks).Average();
            return new TimeSpan((long)averageTicks);
        }
    }

    /// <summary>
    /// Gets current performance metrics
    /// </summary>
    public PerformanceMetrics GetCurrentMetrics()
    {
        lock (_lockObject)
        {
            return new PerformanceMetrics
            {
                AverageCpuUsage = _averageCpuUsage,
                PeakMemoryUsage = _peakMemoryUsage,
                MeasurementCount = _measurementCount,
                OperationAverages = _operationTimes.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Count > 0 ? kvp.Value.Select(t => t.TotalMilliseconds).Average() : 0.0
                )
            };
        }
    }

    /// <summary>
    /// Checks if performance requirements are being met
    /// Requirements: 8.1 (10 second limit), 8.2 (2 second detection), 8.3 (1 second termination start), 8.4 (1% CPU limit)
    /// </summary>
    public PerformanceComplianceReport CheckPerformanceCompliance()
    {
        var report = new PerformanceComplianceReport();
        var metrics = GetCurrentMetrics();
        
        // Check CPU usage requirement (8.4: not more than 1% CPU on average)
        report.CpuCompliant = metrics.AverageCpuUsage <= 1.0;
        report.CpuUsage = metrics.AverageCpuUsage;
        
        // Check operation timing requirements
        if (metrics.OperationAverages.ContainsKey("EmergencyDisconnectSequence"))
        {
            // 8.1: Complete emergency disconnect within 10 seconds
            var emergencyTime = metrics.OperationAverages["EmergencyDisconnectSequence"];
            report.EmergencyDisconnectCompliant = emergencyTime <= 10000; // 10 seconds in ms
            report.EmergencyDisconnectTime = emergencyTime;
        }
        
        if (metrics.OperationAverages.ContainsKey("ConnectionDetection"))
        {
            // 8.2: Detection within 2 seconds
            var detectionTime = metrics.OperationAverages["ConnectionDetection"];
            report.DetectionCompliant = detectionTime <= 2000; // 2 seconds in ms
            report.DetectionTime = detectionTime;
        }
        
        if (metrics.OperationAverages.ContainsKey("TerminationStart"))
        {
            // 8.3: Termination start within 1 second
            var terminationStartTime = metrics.OperationAverages["TerminationStart"];
            report.TerminationStartCompliant = terminationStartTime <= 1000; // 1 second in ms
            report.TerminationStartTime = terminationStartTime;
        }
        
        // Overall compliance
        report.OverallCompliant = report.CpuCompliant && 
                                 report.EmergencyDisconnectCompliant && 
                                 report.DetectionCompliant && 
                                 report.TerminationStartCompliant;
        
        FL.Log($"PerformanceMonitor: Compliance check - Overall: {report.OverallCompliant}, CPU: {report.CpuCompliant} ({report.CpuUsage:F2}%), Emergency: {report.EmergencyDisconnectCompliant} ({report.EmergencyDisconnectTime:F0}ms)");
        
        return report;
    }

    /// <summary>
    /// Collects performance metrics periodically
    /// </summary>
    private void CollectMetrics(object? state)
    {
        try
        {
            // Get current CPU usage
            var currentCpu = _cpuCounter.NextValue();
            
            // Get current memory usage (convert available MB to used bytes approximately)
            var availableMemoryMB = _memoryCounter.NextValue();
            var totalMemoryMB = GetTotalPhysicalMemoryMB();
            var usedMemoryBytes = (long)((totalMemoryMB - availableMemoryMB) * 1024 * 1024);
            
            lock (_lockObject)
            {
                // Update CPU average
                _cpuSamples.Add(currentCpu);
                if (_cpuSamples.Count > 60) // Keep last 5 minutes of samples (60 samples at 5-second intervals)
                {
                    _cpuSamples.RemoveAt(0);
                }
                _averageCpuUsage = _cpuSamples.Average();
                
                // Update memory peak
                _memorySamples.Add(usedMemoryBytes);
                if (_memorySamples.Count > 60)
                {
                    _memorySamples.RemoveAt(0);
                }
                _peakMemoryUsage = _memorySamples.Max();
                
                _measurementCount++;
            }
            
            // Check for performance alerts
            CheckPerformanceAlerts(currentCpu, usedMemoryBytes);
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("CollectMetrics", ex, "Failed to collect performance metrics");
        }
    }

    /// <summary>
    /// Checks for performance alerts and fires events if thresholds are exceeded
    /// </summary>
    private void CheckPerformanceAlerts(double cpuUsage, long memoryUsage)
    {
        var alerts = new List<string>();
        
        // CPU usage alert (requirement 8.4: should not exceed 1% on average)
        if (cpuUsage > 5.0) // Alert at 5% for immediate spikes
        {
            alerts.Add($"High CPU usage: {cpuUsage:F1}%");
        }
        
        // Memory usage alert (alert at 100MB for remote desktop monitoring)
        if (memoryUsage > 100 * 1024 * 1024)
        {
            alerts.Add($"High memory usage: {memoryUsage / (1024 * 1024):F0}MB");
        }
        
        if (alerts.Count > 0)
        {
            var eventArgs = new PerformanceMetricsEventArgs
            {
                CpuUsage = cpuUsage,
                MemoryUsage = memoryUsage,
                Alerts = alerts,
                Timestamp = DateTime.Now
            };
            
            PerformanceAlert?.Invoke(this, eventArgs);
            FL.Log($"PerformanceMonitor: Performance alert - {string.Join(", ", alerts)}");
        }
    }

    /// <summary>
    /// Gets total physical memory in MB using WMI
    /// </summary>
    private double GetTotalPhysicalMemoryMB()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            using var collection = searcher.Get();
            
            foreach (ManagementObject obj in collection)
            {
                var totalBytes = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                return totalBytes / (1024.0 * 1024.0); // Convert to MB
            }
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("GetTotalPhysicalMemory", ex, "Failed to get total physical memory");
        }
        
        return 8192; // Default to 8GB if detection fails
    }

    /// <summary>
    /// Resets all performance metrics
    /// </summary>
    public void ResetMetrics()
    {
        lock (_lockObject)
        {
            _cpuSamples.Clear();
            _memorySamples.Clear();
            _operationTimes.Clear();
            _averageCpuUsage = 0;
            _peakMemoryUsage = 0;
            _measurementCount = 0;
        }
        
        FL.Log("PerformanceMonitor: All metrics reset");
    }

    public void Dispose()
    {
        try
        {
            _monitoringTimer?.Dispose();
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
            FL.Log("PerformanceMonitor: Disposed successfully");
        }
        catch (Exception ex)
        {
            FL.LogDetailedError("PerformanceMonitorDispose", ex, "Error during performance monitor disposal");
        }
    }
}

/// <summary>
/// Performance metrics data structure
/// </summary>
public class PerformanceMetrics
{
    public double AverageCpuUsage { get; set; }
    public long PeakMemoryUsage { get; set; }
    public int MeasurementCount { get; set; }
    public Dictionary<string, double> OperationAverages { get; set; } = new Dictionary<string, double>();
}

/// <summary>
/// Performance compliance report
/// </summary>
public class PerformanceComplianceReport
{
    public bool OverallCompliant { get; set; }
    public bool CpuCompliant { get; set; }
    public double CpuUsage { get; set; }
    public bool EmergencyDisconnectCompliant { get; set; } = true;
    public double EmergencyDisconnectTime { get; set; }
    public bool DetectionCompliant { get; set; } = true;
    public double DetectionTime { get; set; }
    public bool TerminationStartCompliant { get; set; } = true;
    public double TerminationStartTime { get; set; }
}

/// <summary>
/// Performance alert event arguments
/// </summary>
public class PerformanceMetricsEventArgs : EventArgs
{
    public double CpuUsage { get; set; }
    public long MemoryUsage { get; set; }
    public List<string> Alerts { get; set; } = new List<string>();
    public DateTime Timestamp { get; set; }
}