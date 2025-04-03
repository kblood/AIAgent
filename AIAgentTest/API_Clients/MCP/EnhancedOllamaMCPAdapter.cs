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

namespace AIAgentTest.API_Clients.MCP
{
    /// <summary>
    /// Enhanced implementation of Ollama MCP support inspired by the ollama-mcp-bridge project
    /// </summary>
    public class EnhancedOllamaMCPAdapter : IMCPLLMClient
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
        public EnhancedOllamaMCPAdapter(
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
If you need to use a tool, respond in the following format:
<tool_call>
{
  ""tool"": ""tool_name"",
  ""parameters"": {
    ""param1"": ""value1"",
    ""param2"": ""value2""
  }
}
</tool_call>

If you don't need to use a tool, just respond normally.
";
            
            // Combine messages into a full prompt with lower temperature
            var fullPrompt = $"{systemMessage}\n\nAvailable tools:\n{toolsDescription}\n\nUser: {prompt}\n\nAssistant: ";
            
            // Generate response with Ollama with lower temperature for better format compliance
            // Note: We have to use the standard GenerateTextResponseAsync as it doesn't accept parameters directly
            // In a production implementation, we would extend the OllamaClient to support parameters
            var result = await _ollamaClient.GenerateTextResponseAsync(fullPrompt, model);
            
            // Try to parse the response as a tool use
            try
            {
                // Look for tool call block
                var match = Regex.Match(result, @"<tool_call>\s*({.*?})\s*</tool_call>", RegexOptions.Singleline);
                
                if (match.Success)
                {
                    var json = match.Groups[1].Value;
                    var toolCall = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    
                    // Extract tool name and parameters
                    if (toolCall.ContainsKey("tool") && toolCall.ContainsKey("parameters"))
                    {
                        var toolName = toolCall["tool"].ToString();
                        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            toolCall["parameters"].ToString());
                        
                        // Extract text before the tool call as preamble
                        var preamble = result.Substring(0, match.Index).Trim();
                        
                        // If this is an MCP server tool, determine which server it belongs to
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
                                { "raw_response", result },
                                { "server_name", serverName }
                            }
                        };
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
<tool_result>
{formattedResult}
</tool_result>

Based on this tool result, please provide a helpful response to the user's original query.
If you need to use another tool, respond using the tool_call format as instructed previously.";

            // Generate a follow-up response with lower temperature for better format compliance
            var result = await _ollamaClient.GenerateTextResponseAsync(prompt, model);
            
            // Parse for any additional tool calls
            try
            {
                var match = Regex.Match(result, @"<tool_call>\s*({.*?})\s*</tool_call>", RegexOptions.Singleline);
                
                if (match.Success)
                {
                    // We have another tool call
                    var json = match.Groups[1].Value;
                    var toolCall = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    
                    if (toolCall.ContainsKey("tool") && toolCall.ContainsKey("parameters"))
                    {
                        var nextToolName = toolCall["tool"].ToString();
                        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            toolCall["parameters"].ToString());
                        
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
        
        public async Task<string> GenerateWithFunctionsAsync(string prompt, string model, List<FunctionDefinition> functions)
        {
            // Convert FunctionDefinitions to ToolDefinitions and use MCP
            var tools = functions.Select(f => ConvertFunctionToTool(f)).ToList();
            
            var mcpResponse = await GenerateWithMCPAsync(prompt, model, tools);
            
            // Format the result to match what the existing code expects from function calls
            if (mcpResponse.Type == "tool_use")
            {
                return JsonSerializer.Serialize(new { 
                    name = mcpResponse.Tool, 
                    arguments = JsonSerializer.Serialize(mcpResponse.Input) 
                });
            }
            else
            {
                return mcpResponse.Text;
            }
        }
        
        // Helper method to convert FunctionDefinition to ToolDefinition
        private ToolDefinition ConvertFunctionToTool(FunctionDefinition function)
        {
            // Convert parameters to input schema
            var properties = new Dictionary<string, object>();
            var required = new List<string>();
            
            foreach (var param in function.Parameters)
            {
                properties[param.Key] = new Dictionary<string, object>
                {
                    { "type", param.Value.Type.ToLower() },
                    { "description", param.Value.Description }
                };
                
                if (param.Value.Required)
                {
                    required.Add(param.Key);
                }
            }
            
            var inputSchema = new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", properties },
                { "required", required }
            };
            
            // Create output schema (generic)
            var outputSchema = new Dictionary<string, object>
            {
                { "type", "object" },
                { "description", "Function result" }
            };
            
            return new ToolDefinition
            {
                Name = function.Name,
                Description = function.Description,
                Input = inputSchema,
                Output = outputSchema,
                ToolType = "function",
                Tags = new List<string> { "function" }
            };
        }
        
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
                
                builder.AppendLine();
            }
            
            return builder.ToString();
        }
    }
}
