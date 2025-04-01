using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AIAgentTest.Services
{
public class NvidiaSmiUtility
{
    public static async Task<GPUInfo> GetGPUInfoAsync()
    {
        var output = await RunNvidiaSmiCommandAsync();
        return ParseNvidiaSmiOutput(output);
    }

    private static async Task<string> RunNvidiaSmiCommandAsync()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"nvidia-smi exited with code {process.ExitCode}");
        }

        return output;
    }

    private static GPUInfo ParseNvidiaSmiOutput(string output)
    {
        var gpuInfo = new GPUInfo();

        // Parse memory usage
        var memoryRegex = new Regex(@"(\d+)MiB\s*/\s*(\d+)MiB");
        var memoryMatch = memoryRegex.Match(output);
        if (memoryMatch.Success)
        {
            gpuInfo.UsedVRAM = long.Parse(memoryMatch.Groups[1].Value);
            gpuInfo.TotalVRAM = long.Parse(memoryMatch.Groups[2].Value);
            gpuInfo.AvailableVRAM = gpuInfo.TotalVRAM - gpuInfo.UsedVRAM;
        }

        // Parse process information
        var processRegex = new Regex(@"\|\s+0\s+N/A\s+N/A\s+(\d+)\s+(\w+)\s+(.*?)\s+N/A\s+\|");
        var processMatches = processRegex.Matches(output);
        foreach (Match match in processMatches)
        {
            gpuInfo.Processes.Add(new GPUProcess
            {
                PID = int.Parse(match.Groups[1].Value),
                Type = match.Groups[2].Value,
                Name = match.Groups[3].Value.Trim()
            });
        }

        return gpuInfo;
    }
}

public class GPUInfo
{
    public long TotalVRAM { get; set; }
    public long UsedVRAM { get; set; }
    public long AvailableVRAM { get; set; }
    public List<GPUProcess> Processes { get; set; } = new List<GPUProcess>();
}

public class GPUProcess
{
    public int PID { get; set; }
    public string Type { get; set; }
    public string Name { get; set; }
}
}