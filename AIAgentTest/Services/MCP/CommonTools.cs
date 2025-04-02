using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Common tools that can be used with MCP
    /// </summary>
    public class CommonTools
    {
        private readonly HttpClient _httpClient;
        
        /// <summary>
        /// Creates a new CommonTools instance
        /// </summary>
        public CommonTools()
        {
            _httpClient = new HttpClient();
        }
        
        /// <summary>
        /// Registers common tools with the tool registry
        /// </summary>
        /// <param name="toolRegistry">Tool registry to register with</param>
        public void RegisterCommonTools(IToolRegistry toolRegistry)
        {
            // Web search tool
            RegisterWebSearchTool(toolRegistry);
            
            // Get current time tool
            RegisterCurrentTimeTool(toolRegistry);
            
            // Read and write file tools
            RegisterFileTools(toolRegistry);
            
            // Simple calculator tool
            RegisterCalculatorTool(toolRegistry);
        }
        
        /// <summary>
        /// Registers a web search tool
        /// </summary>
        private void RegisterWebSearchTool(IToolRegistry toolRegistry)
        {
            var inputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["query"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The search query"
                    },
                    ["num_results"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "Number of results to return",
                        ["default"] = 5
                    }
                },
                ["required"] = new List<string> { "query" }
            };
            
            var outputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["results"] = new Dictionary<string, object>
                    {
                        ["type"] = "array",
                        ["items"] = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["properties"] = new Dictionary<string, object>
                            {
                                ["title"] = new Dictionary<string, object> { ["type"] = "string" },
                                ["url"] = new Dictionary<string, object> { ["type"] = "string" },
                                ["snippet"] = new Dictionary<string, object> { ["type"] = "string" }
                            }
                        }
                    }
                }
            };
            
            var toolDefinition = new ToolDefinition
            {
                Name = "web_search",
                Description = "Search the web for information",
                Input = inputSchema,
                Output = outputSchema,
                ToolType = "function",
                Tags = new List<string> { "Web", "Search" }
            };
            
            toolRegistry.RegisterTool(toolDefinition, WebSearchHandler);
        }
        
        /// <summary>
        /// Handles web search requests
        /// </summary>
        private async Task<object> WebSearchHandler(Dictionary<string, object> parameters)
        {
            string query = parameters["query"].ToString();
            int numResults = 5;
            
            if (parameters.TryGetValue("num_results", out var numResultsObj))
            {
                numResults = Convert.ToInt32(numResultsObj);
            }
            
            // In a real implementation, this would call a search API
            // For now, we'll return mock results
            await Task.Delay(500); // Simulate network delay
            
            var results = new List<Dictionary<string, string>>();
            
            for (int i = 0; i < numResults; i++)
            {
                results.Add(new Dictionary<string, string>
                {
                    ["title"] = $"Result {i + 1} for {query}",
                    ["url"] = $"https://example.com/result/{i + 1}",
                    ["snippet"] = $"This is a snippet of text from result {i + 1} related to {query}."
                });
            }
            
            return new Dictionary<string, object>
            {
                ["results"] = results
            };
        }
        
        /// <summary>
        /// Registers a current time tool
        /// </summary>
        private void RegisterCurrentTimeTool(IToolRegistry toolRegistry)
        {
            var inputSchema = new Dictionary<string, object>
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
            
            var outputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["current_time"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["timezone"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["unix_timestamp"] = new Dictionary<string, object> { ["type"] = "integer" }
                }
            };
            
            var toolDefinition = new ToolDefinition
            {
                Name = "get_current_time",
                Description = "Get the current date and time",
                Input = inputSchema,
                Output = outputSchema,
                ToolType = "function",
                Tags = new List<string> { "Utility", "Time" }
            };
            
            toolRegistry.RegisterTool(toolDefinition, CurrentTimeHandler);
        }
        
        /// <summary>
        /// Handles current time requests
        /// </summary>
        private async Task<object> CurrentTimeHandler(Dictionary<string, object> parameters)
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
        }
        
        /// <summary>
        /// Registers file tools
        /// </summary>
        private void RegisterFileTools(IToolRegistry toolRegistry)
        {
            // Read file tool
            var readInputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["file_path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Path to the file to read"
                    }
                },
                ["required"] = new List<string> { "file_path" }
            };
            
            var readOutputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["content"] = new Dictionary<string, object> { ["type"] = "string" },
                    ["file_path"] = new Dictionary<string, object> { ["type"] = "string" }
                }
            };
            
            var readToolDefinition = new ToolDefinition
            {
                Name = "read_file",
                Description = "Read the contents of a file",
                Input = readInputSchema,
                Output = readOutputSchema,
                ToolType = "function",
                Tags = new List<string> { "File", "IO" }
            };
            
            toolRegistry.RegisterTool(readToolDefinition, ReadFileHandler);
            
            // Write file tool
            var writeInputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["file_path"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Path to the file to write"
                    },
                    ["content"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "Content to write to the file"
                    }
                },
                ["required"] = new List<string> { "file_path", "content" }
            };
            
            var writeOutputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["success"] = new Dictionary<string, object> { ["type"] = "boolean" },
                    ["file_path"] = new Dictionary<string, object> { ["type"] = "string" }
                }
            };
            
            var writeToolDefinition = new ToolDefinition
            {
                Name = "write_file",
                Description = "Write content to a file",
                Input = writeInputSchema,
                Output = writeOutputSchema,
                ToolType = "function",
                Tags = new List<string> { "File", "IO" }
            };
            
            toolRegistry.RegisterTool(writeToolDefinition, WriteFileHandler);
        }
        
        /// <summary>
        /// Handles read file requests
        /// </summary>
        private async Task<object> ReadFileHandler(Dictionary<string, object> parameters)
        {
            string filePath = parameters["file_path"].ToString();
            
            try
            {
                string content = await File.ReadAllTextAsync(filePath);
                
                return new Dictionary<string, object>
                {
                    ["content"] = content,
                    ["file_path"] = filePath
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    ["error"] = ex.Message,
                    ["file_path"] = filePath
                };
            }
        }
        
        /// <summary>
        /// Handles write file requests
        /// </summary>
        private async Task<object> WriteFileHandler(Dictionary<string, object> parameters)
        {
            string filePath = parameters["file_path"].ToString();
            string content = parameters["content"].ToString();
            
            try
            {
                await File.WriteAllTextAsync(filePath, content);
                
                return new Dictionary<string, object>
                {
                    ["success"] = true,
                    ["file_path"] = filePath
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    ["success"] = false,
                    ["error"] = ex.Message,
                    ["file_path"] = filePath
                };
            }
        }
        
        /// <summary>
        /// Registers a calculator tool
        /// </summary>
        private void RegisterCalculatorTool(IToolRegistry toolRegistry)
        {
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
            
            var toolDefinition = new ToolDefinition
            {
                Name = "calculate",
                Description = "Calculate the result of a mathematical expression",
                Input = inputSchema,
                Output = outputSchema,
                ToolType = "function",
                Tags = new List<string> { "Math", "Utility" }
            };
            
            toolRegistry.RegisterTool(toolDefinition, CalculateHandler);
        }
        
        /// <summary>
        /// Handles calculator requests
        /// </summary>
        private async Task<object> CalculateHandler(Dictionary<string, object> parameters)
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
        }
    }
}