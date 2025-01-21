using AIAgentTest.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class BenchmarkService
{
    private readonly List<BenchmarkResult> _results = new();

    public async Task<BenchmarkResult> RunBenchmark(Func<Task> benchmarkAction, string name)
    {
        var beforeInfo = await NvidiaSmiUtility.GetGPUInfoAsync();
        var startTime = DateTime.Now;

        await benchmarkAction();

        var endTime = DateTime.Now;
        var afterInfo = await NvidiaSmiUtility.GetGPUInfoAsync();

        var result = new BenchmarkResult
        {
            Name = name,
            Duration = endTime - startTime,
            VRAMUsage = new VRAMUsage
            {
                TotalBytes = afterInfo.TotalVRAM - beforeInfo.TotalVRAM,
                UsedBytes = afterInfo.UsedVRAM - beforeInfo.UsedVRAM
            }
        };

        _results.Add(result);
        return result;
    }

    public List<BenchmarkResult> GetResults() => _results;

    public void ClearResults() => _results.Clear();
}

public class BenchmarkResult
{
    public string Name { get; set; }
    public TimeSpan Duration { get; set; }
    public VRAMUsage VRAMUsage { get; set; }
}
