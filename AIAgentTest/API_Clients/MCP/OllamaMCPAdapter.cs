using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Net.Http;
using AIAgentTest.Services.Interfaces;
using AIAgentTest.Services.MCP;
using AIAgentTest.API_Clients;

namespace AIAgentTest.API_Clients.MCP
{
    /// <summary>
    /// Enhanced implementation of Ollama MCP support inspired by the ollama-mcp-bridge project
    /// </summary>
    public class OllamaMCPAdapter : IMCPLLMClient
    {
        private readonly OllamaClient _ollamaClient;
        private readonly IMessageParsingService _parsingService;
        private readonly IToolRegistry _toolRegistry;
        private readonly Dictionary<string, IMCPServerClient> _serverClients = new Dictionary<string, IMCPServerClient>();
        
        /// <summary>
        /// Creates a new EnhancedOllamaMCPAdapter
        /// </summary>
        /// <param name="ollamaClient">The underlying Ollama client</param>
        /// <param name="parsingService">Service for parsing messages</param>
        /// <param name="toolRegistry">Registry of available tools</param>
        public OllamaMCPAdapter(
            OllamaClient ollamaClient, 
            IMessageParsingService parsingService,
            IToolRegistry toolRegistry)
        {
            _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
            _parsingService = parsingService ?? throw new ArgumentNullException(nameof(parsingService));
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        }
        
        /// <summary>
        /// Indicates that this adapter supports MCP
        /// </summary>
        public bool SupportsMCP => true;
        
        /// <summary>
        /// Registers an MCP server with this adapter
        /// </summary>
        /// <param name="serverName">Name of the server</param>
        /// <param name="serverClient">Client for communicating with the server</param>
        public void RegisterMCPServer(string serverName, IMCPServerClient serverClient)
        {
            _serverClients[serverName] = serverClient;
        }

        /// <summary>
        /// Extract tool input parameters from various formats
        /// </summary>
        private Dictionary<string, object> ExtractToolInput(object toolInput)
        {
            try
            {
                // If it's a JsonElement, get the raw text and deserialize
                if (toolInput is JsonElement jsonElement)
                {
                    // Check if it's an empty object
                    if (jsonElement.ValueKind == JsonValueKind.Object && !jsonElement.EnumerateObject().Any())
                    {
                        return new Dictionary<string, object>();
                    }
                    
                    return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                }
                // If it's already a Dictionary, just return it
                else if (toolInput is Dictionary<string, object> dict)
                {
                    return dict;
                }
                // Otherwise, serialize and deserialize to ensure proper format
                else
                {
                    return JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(toolInput));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting tool input: {ex.Message}");
                return new Dictionary<string, object>(); // Return empty dictionary on error
            }
        }
        
        /// <summary>
        /// Create a standardized tool response
        /// </summary>
        private MCPResponse CreateToolResponse(string toolName, Dictionary<string, object> parameters, string preamble, string rawResponse, Dictionary<string, ToolDefinition> toolMap)
        {
            // Check if this is an MCP server tool
            string serverName = null;
            if (toolMap.TryGetValue(toolName, out var toolDefinition) && 
                toolDefinition.Metadata != null &&
                toolDefinition.Metadata.TryGetValue("server_name", out var server))
            {
                serverName = server.ToString();
            }
            
            return new MCPResponse
            {
                Type = "tool_use",
                Tool = toolName,
                Input = parameters,
                Text = preamble, 
                Metadata = new Dictionary<string, object>
                {
                    { "raw_response", rawResponse },
                    { "server_name", serverName }
                }
            };
        }
        
        /// <summary>
        /// Extract JSON from a markdown code block if present
        /// </summary>
        private bool TryExtractJsonFromMarkdown(string input, out string jsonContent)
        {
            var markdownJsonPattern = @"```(?:json)?\s*(\{.*?\})\s*```";
            var markdownMatch = Regex.Match(input, markdownJsonPattern, RegexOptions.Singleline);
            
            if (markdownMatch.Success)
            {
                jsonContent = markdownMatch.Groups[1].Value.Trim();
                Console.WriteLine($"Found JSON in markdown code block: {jsonContent}");
                return true;
            }
            
            jsonContent = input;
            return false;
        }
        
        /// <summary>
        /// Generates a response using Ollama with MCP capabilities
        /// </summary>
        public async Task<MCPResponse> GenerateWithMCPAsync(string prompt, string model, List<ToolDefinition> tools)
        {
            // Create a tool map for quick lookup when parsing responses
            var toolMap = tools.ToDictionary(t => t.Name, t => t);
            
            // Format tools in a clear and structured way
            var toolsDescription = FormatToolDescriptions(tools);
            
            // Create a system message that instructs the model on how to use tools
            var systemMessage = @"You are an AI assistant that can use tools to help answer questions.

If you need to use a tool, respond with a JSON object like this:
{
  ""type"": ""get_date_time"",
  ""tool_input"": {}
}

Notice that the tool name goes directly in the ""type"" field.

For example, to get the current date and time:
{
  ""type"": ""get_date_time"",
  ""tool_input"": {}
}

To read a file:
{
  ""type"": ""read_file"",
  ""tool_input"": {
    ""path"": ""C:\\path\\to\\file.txt""
  }
}

If you don't need to use a tool, just respond normally with text.
";
            
            // Combine messages into a full prompt with lower temperature
            var fullPrompt = $"{systemMessage}\n\nAvailable tools:\n{toolsDescription}\n\nUser: {prompt}\n\nAssistant: ";
            
            // Generate response with Ollama with lower temperature for better format compliance
            var requestParams = new Dictionary<string, object>
            {
                { "temperature", 0.1 }, // Very low temperature for strict format compliance
                { "top_p", 0.9 }
            };
            
            var result = await _ollamaClient.GenerateTextResponseWithParamsAsync(fullPrompt, model, requestParams);
            
            // Log the raw response for debugging
            Console.WriteLine($"Raw response from LLM: {result}");
            
            // Try to parse the response as a tool use
            try
            {
                string jsonToProcess = result;
                string originalResponse = result;
                
                // Check for markdown code blocks with JSON and extract the content
                if (TryExtractJsonFromMarkdown(result, out string extractedJson))
                {
                    jsonToProcess = extractedJson;
                }
                
                // Check if the result is a complete JSON object
                if (jsonToProcess.TrimStart().StartsWith("{") && jsonToProcess.TrimEnd().EndsWith("}"))
                {
                    try
                    {
                        // Try to parse it as a complete JSON object
                        var parsedJson = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonToProcess);
                        
                        // Handle various tool call formats
                        if (parsedJson != null)
                        {
                            // Format 1: {"type": "tool_use", "tool": "tool_name", "tool_input": {...}}
                            if (parsedJson.ContainsKey("type") && 
                                parsedJson["type"].ToString() == "tool_use" &&
                                parsedJson.ContainsKey("tool") &&
                                parsedJson.ContainsKey("tool_input"))
                            {
                                Console.WriteLine("Found standard tool call format");
                                var toolName = parsedJson["tool"].ToString();
                                var parameters = ExtractToolInput(parsedJson["tool_input"]);
                                return CreateToolResponse(toolName, parameters, "", originalResponse, toolMap);
                            }
                            
                            // Format 2: {"type": "tool_name", "tool_input": {...}}
                            else if (parsedJson.ContainsKey("type") && 
                                     parsedJson.ContainsKey("tool_input") &&
                                     toolMap.ContainsKey(parsedJson["type"].ToString()))
                            {
                                Console.WriteLine("Found alternative tool call format (type as tool name)");
                                var toolName = parsedJson["type"].ToString();
                                var parameters = ExtractToolInput(parsedJson["tool_input"]);
                                return CreateToolResponse(toolName, parameters, "", originalResponse, toolMap);
                            }
                            
                            // Format 3: {"function": "tool_name", "parameters": {...}}
                            else if (parsedJson.ContainsKey("function") && 
                                     parsedJson.ContainsKey("parameters") &&
                                     toolMap.ContainsKey(parsedJson["function"].ToString()))
                            {
                                Console.WriteLine("Found function call format");
                                var toolName = parsedJson["function"].ToString();
                                var parameters = ExtractToolInput(parsedJson["parameters"]);
                                return CreateToolResponse(toolName, parameters, "", originalResponse, toolMap);
                            }
                            
                            // Format 4: Direct tool name as type
                            else if (parsedJson.ContainsKey("type") && 
                                    toolMap.ContainsKey(parsedJson["type"].ToString()) &&
                                    !parsedJson.ContainsKey("tool"))
                            {
                                Console.WriteLine("Found direct tool name as type format");
                                var toolName = parsedJson["type"].ToString();
                                
                                // Create empty parameters if not provided
                                Dictionary<string, object> parameters = new Dictionary<string, object>();
                                if (parsedJson.ContainsKey("tool_input"))
                                {
                                    parameters = ExtractToolInput(parsedJson["tool_input"]);
                                }
                                
                                return CreateToolResponse(toolName, parameters, "", originalResponse, toolMap);
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Failed to parse as complete JSON: {ex.Message}");
                        // Continue to regex parsing
                    }
                }
                
                // Fall back to regex for other patterns
                var jsonPattern = @"\{\s*""type""\s*:\s*""tool_use"".*?\}"; // Match JSON with tool_use type
                var match = Regex.Match(result, jsonPattern, RegexOptions.Singleline);
                
                if (match.Success)
                {
                    var json = match.Value;
                    Console.WriteLine($"Found tool call using regex: {json}");
                    var toolCall = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    
                    // Extract tool name and parameters
                    if (toolCall.ContainsKey("tool") && toolCall.ContainsKey("tool_input"))
                    {
                        var toolName = toolCall["tool"].ToString();
                        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            toolCall["tool_input"].ToString());
                        
                        // Extract text before the tool call as preamble
                        var preamble = result.Substring(0, match.Index).Trim();
                        
                        return CreateToolResponse(toolName, parameters, preamble, result, toolMap);
                    }
                }
                
                // If no tool call block or invalid format, return as text
                return new MCPResponse
                {
                    Type = "text",
                    Text = result
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing tool call: {ex.Message}");
                // If parsing fails, treat as regular text
                return new MCPResponse
                {
                    Type = "text",
                    Text = result,
                    Metadata = new Dictionary<string, object>
                    {
                        { "error", ex.Message }
                    }
                };
            }
        }
        
        /// <summary>
        /// Continues the conversation with a tool result
        /// </summary>
        public async Task<MCPResponse> ContinueWithToolResultAsync(
            string originalPrompt, 
            string toolName, 
            object toolResult, 
            string model)
        {
            // Format the tool result as a system message
            var formattedResult = JsonSerializer.Serialize(toolResult, new JsonSerializerOptions { WriteIndented = true });
            
            var prompt = $@"Previous user query: {originalPrompt}

You used the {toolName} tool, which returned the following result:

```
{formattedResult}
```

Based on this information, please provide a helpful and natural response to the user's original query.
If you need to use another tool, respond using the JSON format. Otherwise, respond conversationally.

Do NOT repeat all the raw data from the tool result. Instead, present the information in a natural, helpful way.

User: {originalPrompt}
Assistant:";

            // Generate a follow-up response with lower temperature for consistent formatting
            var requestParams = new Dictionary<string, object>
            {
                { "temperature", 0.1 }, // Very low temperature for strict format compliance
                { "top_p", 0.9 }
            };
            
            var result = await _ollamaClient.GenerateTextResponseWithParamsAsync(prompt, model, requestParams);
            
            // Parse for any additional tool calls
            try
            {
                string jsonToProcess = result;
                string originalResponse = result;
                
                // Check for markdown code blocks with JSON and extract the content
                if (TryExtractJsonFromMarkdown(result, out string extractedJson))
                {
                    jsonToProcess = extractedJson;
                }
                
                // Check if the response is a complete JSON object
                if (jsonToProcess.TrimStart().StartsWith("{") && jsonToProcess.TrimEnd().EndsWith("}"))
                {
                    try
                    {
                        // Try to parse it as a complete JSON object
                        var toolCall = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonToProcess);
                        
                        // Check if it's a valid tool call
                        if (toolCall != null && 
                            toolCall.ContainsKey("type") && 
                            toolCall["type"].ToString() == "tool_use" &&
                            toolCall.ContainsKey("tool") &&
                            toolCall.ContainsKey("tool_input"))
                        {
                            Console.WriteLine("Found complete tool call JSON in continuation");
                            
                            // Extract tool name and parameters
                            var nextToolName = toolCall["tool"].ToString();
                            Dictionary<string, object> parameters;
                            
                            // Handle different formats of tool_input
                            if (toolCall["tool_input"] is JsonElement jsonElement)
                            {
                                parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                    jsonElement.GetRawText());
                            }
                            else
                            {
                                parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                    JsonSerializer.Serialize(toolCall["tool_input"]));
                            }
                            
                            // Check if the tool belongs to an MCP server
                            var toolDefinition = _toolRegistry.GetToolDefinition(nextToolName);
                            string serverName = null;
                            
                            if (toolDefinition?.Metadata != null && 
                                toolDefinition.Metadata.TryGetValue("server_name", out var server))
                            {
                                serverName = server.ToString();
                            }
                            
                            return new MCPResponse
                            {
                                Type = "tool_use",
                                Tool = nextToolName,
                                Input = parameters,
                                Text = "", // No preamble in this case
                                Metadata = new Dictionary<string, object>
                                {
                                    { "previous_tool", toolName },
                                    { "previous_result", toolResult },
                                    { "raw_response", originalResponse },
                                    { "server_name", serverName }
                                }
                            };
                        }
                        
                        // Format 2: {"type": "tool_name", "tool_input": {...}}
                        else if (toolCall.ContainsKey("type") && 
                                 toolCall.ContainsKey("tool_input") &&
                                 _toolRegistry.ToolExists(toolCall["type"].ToString()))
                        {
                            Console.WriteLine("Found alternative tool call format (type as tool name) in continuation");
                            var nextToolName = toolCall["type"].ToString();
                            var parameters = ExtractToolInput(toolCall["tool_input"]);
                            
                            // Check if the tool belongs to an MCP server
                            var toolDefinition = _toolRegistry.GetToolDefinition(nextToolName);
                            string serverName = null;
                            
                            if (toolDefinition?.Metadata != null && 
                                toolDefinition.Metadata.TryGetValue("server_name", out var server))
                            {
                                serverName = server.ToString();
                            }
                            
                            return new MCPResponse
                            {
                                Type = "tool_use",
                                Tool = nextToolName,
                                Input = parameters,
                                Text = "", // No preamble in this case
                                Metadata = new Dictionary<string, object>
                                {
                                    { "previous_tool", toolName },
                                    { "previous_result", toolResult },
                                    { "raw_response", originalResponse },
                                    { "server_name", serverName }
                                }
                            };
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Failed to parse continuation as complete JSON: {ex.Message}");
                        // Continue to regex parsing
                    }
                }
                
                // Look for JSON tool usage in the response with regex as fallback
                var jsonPattern = @"\{\s*""type""\s*:\s*""tool_use"".*?\}"; // Match JSON with tool_use type
                var match = Regex.Match(result, jsonPattern, RegexOptions.Singleline);
                
                if (match.Success)
                {
                    // We have another tool call
                    var json = match.Value;
                    var toolCall = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    
                    if (toolCall.ContainsKey("tool") && toolCall.ContainsKey("tool_input"))
                    {
                        var nextToolName = toolCall["tool"].ToString();
                        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            toolCall["tool_input"].ToString());
                        
                        // Extract text before the tool call as preamble
                        var preamble = result.Substring(0, match.Index).Trim();
                        
                        // Check if the tool belongs to an MCP server
                        var toolDefinition = _toolRegistry.GetToolDefinition(nextToolName);
                        string serverName = null;
                        
                        if (toolDefinition?.Metadata != null && 
                            toolDefinition.Metadata.TryGetValue("server_name", out var server))
                        {
                            serverName = server.ToString();
                        }
                        
                        return new MCPResponse
                        {
                            Type = "tool_use",
                            Tool = nextToolName,
                            Input = parameters,
                            Text = preamble,
                            Metadata = new Dictionary<string, object>
                            {
                                { "previous_tool", toolName },
                                { "previous_result", toolResult },
                                { "raw_response", result },
                                { "server_name", serverName }
                            }
                        };
                    }
                }
                
                // No additional tool call, just return the text response
                return new MCPResponse
                {
                    Type = "text",
                    Text = result
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing tool call: {ex.Message}");
                // If parsing fails, return as text
                return new MCPResponse
                {
                    Type = "text",
                    Text = result,
                    Metadata = new Dictionary<string, object>
                    {
                        { "error", ex.Message }
                    }
                };
            }
        }
        
        /// <summary>
        /// Generates a response with context from previous MCP interactions
        /// </summary>
        public async Task<string> GenerateWithContextAsync(
            string prompt, 
            List<MCPContextMessage> contextMessages, 
            string model)
        {
            // Format context messages
            var contextBuilder = new StringBuilder();
            
            foreach (var message in contextMessages)
            {
                switch (message.Type)
                {
                    case "tool_use":
                        contextBuilder.AppendLine($"You used the {message.ToolName} tool with parameters:");
                        contextBuilder.AppendLine(JsonSerializer.Serialize(message.Input, new JsonSerializerOptions { WriteIndented = true }));
                        contextBuilder.AppendLine();
                        break;
                        
                    case "tool_result":
                        contextBuilder.AppendLine($"The {message.ToolName} tool returned:");
                        if (message.Success)
                        {
                            contextBuilder.AppendLine(JsonSerializer.Serialize(message.Result, new JsonSerializerOptions { WriteIndented = true }));
                        }
                        else
                        {
                            contextBuilder.AppendLine($"Error: {message.Error}");
                        }
                        contextBuilder.AppendLine();
                        break;
                        
                    case "retrieval_request":
                        contextBuilder.AppendLine("Retrieval Request:");
                        contextBuilder.AppendLine($"Query: {message.Input["query"]}");
                        if (message.Input.ContainsKey("source") && message.Input["source"] != null)
                        {
                            contextBuilder.AppendLine($"Source: {message.Input["source"]}");
                        }
                        break;
                        
                    case "retrieval_result":
                        contextBuilder.AppendLine("Retrieval Result:");
                        contextBuilder.AppendLine($"Source: {message.Input["source"]}");
                        contextBuilder.AppendLine($"Result: {JsonSerializer.Serialize(message.Result)}");
                        break;
                }
            }
            
            // Complete prompt with context
            var fullPrompt = $@"Context from previous interactions:
{contextBuilder}

Current user query: {prompt}

Please respond based on this context and the current query.";

            // Generate response
            return await _ollamaClient.GenerateTextResponseAsync(fullPrompt, model);
        }
        
        /// <summary>
        /// Executes a tool, whether it's local or on an MCP server
        /// </summary>
        public async Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters, string serverName = null)
        {
            // If a server name is provided, route to that server
            if (!string.IsNullOrEmpty(serverName) && _serverClients.TryGetValue(serverName, out var serverClient))
            {
                return await serverClient.ExecuteToolAsync(toolName, parameters);
            }
            
            // Otherwise use the local tool registry
            var handler = _toolRegistry.GetToolHandler(toolName);
            if (handler != null)
            {
                return await handler(parameters);
            }
            
            throw new Exception($"Tool '{toolName}' not found");
        }
        
        // ILLMClient interface implementation - forwarding to the underlying client
        public Task<List<string>> GetAvailableModelsAsync()
        {
            return _ollamaClient.GetAvailableModelsAsync();
        }
        
        public Task<string> GenerateTextResponseAsync(string prompt, string model = null)
        {
            return _ollamaClient.GenerateTextResponseAsync(prompt, model);
        }
        
        public IAsyncEnumerable<string> GenerateStreamResponseAsync(string prompt, string model = null)
        {
            return _ollamaClient.GenerateStreamResponseAsync(prompt, model);
        }
        
        public Task<string> GenerateResponseWithImageAsync(string prompt, string imagePath, string model = null)
        {
            return _ollamaClient.GenerateResponseWithImageAsync(prompt, imagePath, model);
        }
        
        public Task LoadModelAsync(string modelName)
        {
            return _ollamaClient.LoadModelAsync(modelName);
        }
        
        public Task<ModelInfo> GetModelInfoAsync(string modelName)
        {
            return _ollamaClient.GetModelInfoAsync(modelName);
        }
        
        public Task<string> GenerateWithFunctionsAsync(string prompt, string model, List<FunctionDefinition> functions)
        {
            // Use the extension method to implement this function
            return this.GenerateWithFunctionsAsyncImpl(prompt, model, functions);
        }
        
        // Helper method removed - now using GenerateWithFunctionsAsyncExtension.ConvertFunctionToTool
        
        /// <summary>
        /// Format tool descriptions in a way that's clear for LLMs
        /// </summary>
        private string FormatToolDescriptions(List<ToolDefinition> tools)
        {
            var builder = new StringBuilder();
            
            foreach (var tool in tools)
            {
                builder.AppendLine($"## {tool.Name}");
                builder.AppendLine($"Description: {tool.Description}");
                
                // Format input parameters
                builder.AppendLine("Parameters:");
                if (tool.Input != null && tool.Input.ContainsKey("properties"))
                {
                    var properties = (Dictionary<string, object>)tool.Input["properties"];
                    var required = tool.Input.ContainsKey("required") 
                        ? (List<string>)tool.Input["required"] 
                        : new List<string>();
                    
                    foreach (var param in properties)
                    {
                        var paramName = param.Key;
                        var paramDetails = (Dictionary<string, object>)param.Value;
                        var isRequired = required.Contains(paramName) ? "required" : "optional";
                        
                        builder.AppendLine($"- {paramName} ({isRequired}): {paramDetails["description"]}");
                    }
                }
                
                builder.AppendLine();
            }
            
            return builder.ToString();
        }

        public Task<string> GenerateWithFunctionsAsync(string prompt, string model, List<API_Clients.FunctionDefinition> functions)
        {
            throw new NotImplementedException();
        }
    }
}