using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIAgentTest.API_Clients;
using AIAgentTest.API_Clients.MCP;
using AIAgentTest.Services.MCP;
using AIAgentTest.Services.Interfaces;

namespace AIAgentTest
{
    // Simple test class to demonstrate MCP functionality
    public class TestMCPImplementation
    {
        public static async Task RunTest()
        {
            Console.WriteLine("Testing MCP Implementation...");
            
            // Create tool registry
            var toolRegistry = new ToolRegistry();
            
            // Register some simple tools
            RegisterTestTools(toolRegistry);
            
            // Create Ollama client
            var ollamaClient = new OllamaClient();
            
            // Create message parsing service (needed for OllamaMCPAdapter)
            // In a real application, this would be properly initialized
            var messageParsingService = new MessageParsingService();
            
            // Create enhanced adapter
            var enhancedAdapter = new EnhancedOllamaMCPAdapter(ollamaClient, messageParsingService, toolRegistry);
            
            // Create a file system server client
            var fileSystemServer = new FileSystemMCPServerClient("http://localhost:3000");
            
            // Register server
            enhancedAdapter.RegisterMCPServer("filesystem", fileSystemServer);
            
            // Get available tools
            var tools = toolRegistry.GetTools();
            Console.WriteLine($"Available tools: {tools.Count}");
            foreach (var tool in tools)
            {
                Console.WriteLine($"- {tool.Name}: {tool.Description}");
            }
            
            // Try a simple query
            Console.WriteLine("\nSending query to model...");
            try
            {
                var response = await enhancedAdapter.GenerateWithMCPAsync(
                    "What is the square root of 144?", 
                    "llama3", 
                    tools);
                
                Console.WriteLine($"Response type: {response.Type}");
                
                if (response.Type == "tool_use")
                {
                    Console.WriteLine($"Tool: {response.Tool}");
                    Console.WriteLine($"Parameters: {System.Text.Json.JsonSerializer.Serialize(response.Input)}");
                    
                    // Execute the tool
                    try
                    {
                        var toolResult = await enhancedAdapter.ExecuteToolAsync(response.Tool, response.Input);
                        Console.WriteLine($"Tool result: {toolResult}");
                        
                        // Continue the conversation
                        var continuedResponse = await enhancedAdapter.ContinueWithToolResultAsync(
                            "What is the square root of 144?",
                            response.Tool,
                            toolResult,
                            "llama3");
                        
                        Console.WriteLine($"Final response: {continuedResponse.Text}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error executing tool: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"Text response: {response.Text}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            
            Console.WriteLine("Test complete!");
        }
        
        private static void RegisterTestTools(ToolRegistry toolRegistry)
        {
            // Register a simple calculator tool
            var inputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["expression"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Math expression to evaluate (e.g., '2 * (3 + 4)')"
                    }
                },
                ["required"] = new List<string> { "expression" }
            };
            
            var outputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["result"] = new Dictionary<string, object> { ["type"] = "number" },
                    ["expression"] = new Dictionary<string, object> { ["type"] = "string" }
                }
            };
            
            toolRegistry.RegisterTool(
                "calculate",
                "Calculate the result of a mathematical expression",
                inputSchema,
                outputSchema,
                async (parameters) => 
                {
                    string expression = parameters["expression"].ToString();
                    
                    try
                    {
                        // Create a new DataTable instance for calculation
                        var dataTable = new System.Data.DataTable();
                        var result = dataTable.Compute(expression, "");
                        
                        return new Dictionary<string, object>
                        {
                            ["result"] = Convert.ToDouble(result),
                            ["expression"] = expression
                        };
                    }
                    catch (Exception ex)
                    {
                        return new Dictionary<string, object>
                        {
                            ["error"] = ex.Message,
                            ["expression"] = expression
                        };
                    }
                },
                "function",
                new List<string> { "Math", "Utility" }
            );
            
            // Register current time tool
            var timeInputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["timezone"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Optional timezone (default: local)",
                        ["default"] = "local"
                    }
                }
            };
            
            var timeOutputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["current_time"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["timezone"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["unix_timestamp"] = new Dictionary<string, object> { ["type"] = "integer" }
                }
            };
            
            toolRegistry.RegisterTool(
                "get_current_time",
                "Get the current date and time",
                timeInputSchema,
                timeOutputSchema,
                async (parameters) => 
                {
                    string timezone = "local";
                    
                    if (parameters.TryGetValue("timezone", out var timezoneObj))
                    {
                        timezone = timezoneObj.ToString();
                    }
                    
                    DateTime now = DateTime.Now;
                    DateTimeOffset nowOffset = DateTimeOffset.Now;
                    
                    if (timezone != "local")
                    {
                        try
                        {
                            var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                            now = TimeZoneInfo.ConvertTime(now, timeZoneInfo);
                            nowOffset = TimeZoneInfo.ConvertTime(nowOffset, timeZoneInfo);
                        }
                        catch
                        {
                            // If timezone not found, fall back to local
                        }
                    }
                    
                    return new Dictionary<string, object>
                    {
                        ["current_time"] = now.ToString("yyyy-MM-dd HH:mm:ss"),
                        ["timezone"] = timezone,
                        ["unix_timestamp"] = (int)nowOffset.ToUnixTimeSeconds()
                    };
                },
                "function",
                new List<string> { "Utility", "Time" }
            );
            
            // Register weather tool (mock implementation)
            var weatherInputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["location"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "City or location to get weather for"
                    }
                },
                ["required"] = new List<string> { "location" }
            };
            
            var weatherOutputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["location"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["temperature"] = new Dictionary<string, object> { ["type"] = "number" },
                    ["condition"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["humidity"] = new Dictionary<string, object> { ["type"] = "number" }
                }
            };
            
            toolRegistry.RegisterTool(
                "get_weather",
                "Get current weather for a location",
                weatherInputSchema,
                weatherOutputSchema,
                async (parameters) => 
                {
                    string location = parameters["location"].ToString();
                    
                    // This is a mock implementation
                    var random = new Random();
                    var temperature = random.Next(0, 35); // 0-35°C
                    var humidity = random.Next(30, 100); // 30-100%
                    
                    var conditions = new[] { "Sunny", "Cloudy", "Rainy", "Partly Cloudy", "Stormy" };
                    var condition = conditions[random.Next(0, conditions.Length)];
                    
                    return new Dictionary<string, object>
                    {
                        ["location"] = location,
                        ["temperature"] = temperature,
                        ["condition"] = condition,
                        ["humidity"] = humidity
                    };
                },
                "function",
                new List<string> { "Weather", "Utility" }
            );
        }
    }
}
