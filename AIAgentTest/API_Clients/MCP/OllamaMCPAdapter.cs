using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AIAgentTest.Services.Interfaces;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.API_Clients.MCP
{
    /// <summary>
    /// Adapter to provide MCP capabilities for Ollama models
    /// </summary>
    public class OllamaMCPAdapter : IMCPLLMClient
    {
        private readonly OllamaClient _ollamaClient;
        private readonly IMessageParsingService _parsingService;
        
        /// <summary>
        /// Creates a new OllamaMCPAdapter
        /// </summary>
        /// <param name="ollamaClient">The underlying Ollama client</param>
        /// <param name="parsingService">Service for parsing messages</param>
        public OllamaMCPAdapter(OllamaClient ollamaClient, IMessageParsingService parsingService)
        {
            _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
            _parsingService = parsingService ?? throw new ArgumentNullException(nameof(parsingService));
        }
        
        /// <summary>
        /// Indicates that this adapter supports MCP
        /// </summary>
        public bool SupportsMCP => true; // We're providing MCP support via the adapter
        
        /// <summary>
        /// Generates a response using Ollama with MCP-like tool capabilities
        /// </summary>
        public async Task<MCPResponse> GenerateWithMCPAsync(string prompt, string model, List<ToolDefinition> tools)
        {
            // Create a tool-aware prompt with special format for tool detection
            var toolsDescription = new StringBuilder();
            
            toolsDescription.AppendLine("You have access to the following tools:");
            
            foreach (var tool in tools)
            {
                toolsDescription.AppendLine($"## {tool.Name}");
                toolsDescription.AppendLine($"Description: {tool.Description}");
                
                // Format input parameters
                toolsDescription.AppendLine("Parameters:");
                var properties = (Dictionary<string, object>)tool.Input["properties"];
                var required = tool.Input.ContainsKey("required") 
                    ? (List<string>)tool.Input["required"] 
                    : new List<string>();
                    
                foreach (var param in properties)
                {
                    var paramName = param.Key;
                    var paramDetails = (Dictionary<string, object>)param.Value;
                    var isRequired = required.Contains(paramName) ? "required" : "optional";
                    
                    toolsDescription.AppendLine($"- {paramName} ({isRequired}): {paramDetails["description"]}");
                }
                
                toolsDescription.AppendLine();
            }
            
            toolsDescription.AppendLine("IMPORTANT: If you need to use a tool, your response MUST be in the exact format:");
            toolsDescription.AppendLine("I'll use the [TOOL_NAME] tool with these parameters:");
            toolsDescription.AppendLine("<tool_call>");
            toolsDescription.AppendLine("{");
            toolsDescription.AppendLine("  \"tool\": \"tool_name\",");
            toolsDescription.AppendLine("  \"parameters\": {");
            toolsDescription.AppendLine("    \"param1\": \"value1\",");
            toolsDescription.AppendLine("    \"param2\": \"value2\"");
            toolsDescription.AppendLine("  }");
            toolsDescription.AppendLine("}");
            toolsDescription.AppendLine("</tool_call>");
            toolsDescription.AppendLine("Wait for the tool to run and return results. DO NOT make up tool results.");
            toolsDescription.AppendLine();
            toolsDescription.AppendLine("If you don't need to use a tool, respond normally without the tool_call format.");
            
            // Combine with user prompt
            var fullPrompt = $"{toolsDescription}\n\nUser: {prompt}\n\nAssistant: ";
            
            // Generate response with Ollama
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
                        
                        return new MCPResponse
                        {
                            Type = "tool_use",
                            Tool = toolName,
                            Input = parameters,
                            Text = preamble, // Retain any explanatory text
                            Metadata = new Dictionary<string, object>
                            {
                                { "raw_response", result }
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
            catch
            {
                // If parsing fails, treat as regular text
                return new MCPResponse
                {
                    Type = "text",
                    Text = result
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
If you need to use another tool, respond in the tool_call format as instructed previously.";

            // Generate a follow-up response
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
                                { "raw_response", result }
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
            catch
            {
                // If parsing fails, return as text
                return new MCPResponse
                {
                    Type = "text",
                    Text = result
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
                        
                    // Add other context message types as needed
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
    }
}