public class PerformanceMetrics
{
    public DateTime Timestamp { get; set; }
    public string ModelName { get; set; }
    public double ResponseTimeMs { get; set; }
    public long MemoryUsageBytes { get; set; }
    public int TokenCount { get; set; }
    public int ContextWindowUsed { get; set; }
    public double GpuUtilization { get; set; }
    public string PromptText { get; set; }
}

public class PerformanceLogger
{
    private readonly string _logPath = "performance_logs.json";

    public void LogMetrics(PerformanceMetrics metrics)
    {
        var logs = LoadExistingLogs();
        logs.Add(metrics);
        File.WriteAllText(_logPath, JsonSerializer.Serialize(logs));
    }

    private List<PerformanceMetrics> LoadExistingLogs()
    {
        if (!File.Exists(_logPath)) return new List<PerformanceMetrics>();
        return JsonSerializer.Deserialize<List<PerformanceMetrics>>(File.ReadAllText(_logPath));
    }
}
