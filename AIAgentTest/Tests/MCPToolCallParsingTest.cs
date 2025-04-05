using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using AIAgentTest.API_Clients.MCP;
using AIAgentTest.API_Clients;
using AIAgentTest.Services.MCP;
using AIAgentTest.Services.Interfaces;
// Use fully qualified names to avoid ambiguity
// using AIAgentTest.Common;
using Moq;

namespace AIAgentTest.Tests
{
    /// <summary>
    /// Test class to verify MCP tool call parsing functionality
    /// </summary>
    public class MCPToolCallParsingTest
    {
        /// <summary>
        /// Validates that the OllamaMCPAdapter correctly handles different tool call formats
        /// </summary>
        public static async Task ValidateToolCallParsing()
        {
            // Create mocks
            var mockOllamaClient = new Mock<OllamaClient>(string.Empty);
            var mockParsingService = new Mock<IMessageParsingService>();
            var mockToolRegistry = new Mock<IToolRegistry>();
            
            // Setup tool registry mock
            var toolDefinition = new ToolDefinition
            {
                Name = "get_date_time",
                Description = "Get the current date and time",
                Parameters = new Dictionary<string, AIAgentTest.Services.MCP.MCPParameterDefinition>()
            };
            
            mockToolRegistry.Setup(m => m.GetToolDefinition("get_date_time")).Returns(toolDefinition);
            
            // List of test formats
            var testFormats = new Dictionary<string, string>
            {
                // Direct JSON: Llama 3.2 format
                { "Direct JSON", @"{
  ""type"": ""get_date_time"",
  ""tool_input"": {}
}" },
                
                // Markdown code block: Qwen/Gemma format
                { "Markdown Code Block", @"```json
{
  ""type"": ""get_date_time"",
  ""tool_input"": {}
}
```" },
                
                // Mixed format with text before
                { "Mixed Format", @"I need to check the current time:

```json
{
  ""type"": ""get_date_time"",
  ""tool_input"": {}
}
```" },
                
                // Standard tool_use format
                { "Standard Format", @"{
  ""type"": ""tool_use"",
  ""tool"": ""get_date_time"",
  ""tool_input"": {}
}" }
            };
            
            // Create adapter instance
            var adapter = new OllamaMCPAdapter(
                mockOllamaClient.Object,
                mockParsingService.Object,
                mockToolRegistry.Object);
            
            // Test each format
            foreach (var format in testFormats)
            {
                try
                {
                    // Setup mock to return the current test format
                    mockOllamaClient
                        .Setup(m => m.GenerateTextResponseWithParamsAsync(
                            It.IsAny<string>(), 
                            It.IsAny<string>(), 
                            It.IsAny<Dictionary<string, object>>()))
                        .ReturnsAsync(format.Value);
                    
                    // Call the method with any prompt and empty tools list
                    var result = await adapter.GenerateWithMCPAsync(
                        "What time is it?", 
                        "test-model", 
                        new List<ToolDefinition> { toolDefinition });
                    
                    // Validate the result
                    if (result.Type != "tool_use" || result.Tool != "get_date_time")
                    {
                        Console.WriteLine($"FAILED: {format.Key} - Expected tool_use with get_date_time tool, but got {result.Type} with {result.Tool} tool");
                    }
                    else
                    {
                        Console.WriteLine($"PASSED: {format.Key} - Correctly parsed tool call");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: {format.Key} - Exception: {ex.Message}");
                }
            }
            
            Console.WriteLine("Tool call parsing test completed");
        }
    }
}
