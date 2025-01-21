using AIAgentTest.API_Clients;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIAgentTest.Services;

public class BenchmarkTests
{
    private readonly OllamaClient _ollamaClient;
    private readonly BenchmarkService _benchmarkService;

    public BenchmarkTests(OllamaClient ollamaClient, BenchmarkService benchmarkService)
    {
        _ollamaClient = ollamaClient;
        _benchmarkService = benchmarkService;
    }

    public async Task RunAllTests()
    {
        await RunTextGenerationTest();
        await RunImageGenerationTest();
        await RunFunctionCallingTest();
    }

    private async Task RunTextGenerationTest()
    {
        await _benchmarkService.RunBenchmark(async () =>
        {
            await _ollamaClient.GenerateTextResponseAsync(
                "Write a short story about a robot learning to love.",
                "llama2:latest"
            );
        }, "Text Generation");
    }

    private async Task RunImageGenerationTest()
    {
        await _benchmarkService.RunBenchmark(async () =>
        {
            await _ollamaClient.GenerateResponseWithImageAsync(
                "Describe this image in detail.",
                "test_image.jpg",
                "llava:latest"
            );
        }, "Image Generation");
    }

    private async Task RunFunctionCallingTest()
    {
        var functions = new List<FunctionDefinition>
        {
            new FunctionDefinition
            {
                Name = "test_function",
                Description = "A test function",
                Parameters = new Dictionary<string, ParameterDefinition>
                {
                    ["param1"] = new ParameterDefinition
                    {
                        Type = "string",
                        Description = "Test parameter",
                        Required = true
                    }
                }
            }
        };

        await _benchmarkService.RunBenchmark(async () =>
        {
            await _ollamaClient.GenerateWithFunctionsAsync(
                "Call the test function with 'hello' as parameter.",
                "llama2:latest",
                functions
            );
        }, "Function Calling");
    }
}
