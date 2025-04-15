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
            string? serverName = null;
            if (toolMap.TryGetValue(toolName, out var toolDefinition) && 
                toolDefinition.Metadata != null &&
                toolDefinition.Metadata.TryGetValue("server_name", out var server))
            {
                serverName = server?.ToString();
            }
            
            var metadata = new Dictionary<string, object>
            {
                { "raw_response", rawResponse }
            };
            
            // Only add server_name if it's not null
            if (serverName != null)
            {
                metadata["server_name"] = serverName;
            }
            
            return new MCPResponse
            {
                Type = "tool_use",
                Tool = toolName,
                Input = parameters,
                Text = preamble,
                Metadata = metadata
            };
        }
        
        /// <summary>
        /// Extract JSON from a markdown code block if present
        /// </summary>
        private bool TryExtractJsonFromMarkdown(string input, out string jsonContent)
        {
            // More robust pattern to handle incomplete markdown code blocks and various formats
            var markdownJsonPattern = @"```(?:json)?\s*(\{[\s\S]*?(?:\}|$))";
            var markdownMatch = Regex.Match(input, markdownJsonPattern, RegexOptions.Singleline);
            
            if (markdownMatch.Success)
            {
                var extractedJson = markdownMatch.Groups[1].Value.Trim();
                
                // Make sure we have a complete JSON object by checking for matching braces
                int openBraces = 0;
                int closeBraces = 0;
                foreach (char c in extractedJson)
                {
                    if (c == '{') openBraces++;
                    if (c == '}') closeBraces++;
                }
                
                // If JSON is incomplete, try to fix it
                if (openBraces > closeBraces)
                {
                    // Add missing closing braces
                    extractedJson += new string('}', openBraces - closeBraces);
                    Console.WriteLine($"Fixed incomplete JSON by adding closing braces: {extractedJson}");
                }
                
                jsonContent = extractedJson;
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
            
            // Prepare examples with user document path
            string userDocsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            userDocsPath = userDocsPath.Replace("\\", "\\\\"); // Escape backslashes for JSON
            
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

            try
            {
                // Combine messages into a full prompt with lower temperature
                var fullPrompt = $"{systemMessage}\n\nAvailable tools:\n{toolsDescription}\n\nUser: {prompt}\n\nAssistant: ";
                
                // Use settings from the user preferences instead of hardcoded values
                var requestParams = new Dictionary<string, object>
                {
                    { "temperature", Properties.Settings.Default.Temperature },
                    { "top_p", Properties.Settings.Default.TopP },
                    { "frequency_penalty", Properties.Settings.Default.FrequencyPenalty },
                    { "presence_penalty", Properties.Settings.Default.PresencePenalty },
                    // Add max context length and max response length
                    { "num_ctx", Properties.Settings.Default.MaxContextLength },
                    { "num_predict", Properties.Settings.Default.MaxResponseLength }
                };

                // Use streaming approach to detect tool calls early
                var responseBuilder = new StringBuilder();
                var jsonBuilder = new StringBuilder();
                bool inJsonBlock = false;
                bool toolCallDetected = false;
                MCPResponse toolCallResponse = null;
                
                // Stream the response and look for tool calls on-the-fly
                await foreach (var chunk in _ollamaClient.GenerateStreamResponseWithParamsAsync(fullPrompt, model, requestParams))
                {
                    responseBuilder.Append(chunk);
                    string currentResponse = responseBuilder.ToString();
                    Console.WriteLine($"Received chunk: {chunk}");
                    
                    // Special case: Check if we have a markdown code block with JSON, even if incomplete
                    if (currentResponse.Contains("```json") || currentResponse.Contains("```"))
                    {
                        // Try to extract JSON from markdown code block
                        if (TryExtractJsonFromMarkdown(currentResponse, out string markdownJson))
                        {
                            Console.WriteLine($"Extracted JSON from markdown during streaming: {markdownJson}");
                            var parsedResponse = TryParseToolCall(markdownJson, toolMap, currentResponse);
                            if (parsedResponse != null)
                            {
                                toolCallResponse = parsedResponse;
                                toolCallDetected = true;
                                break;
                            }
                        }
                    }
                    
                    // Look for JSON blocks in markdown format (old code, keeping for backup)
                    if (chunk.Contains("```json") || chunk.Contains("```") && !inJsonBlock)
                    {
                        inJsonBlock = true;
                        jsonBuilder.Clear();
                        continue;
                    }
                    
                    if (inJsonBlock)
                    {
                        if (chunk.Contains("```"))
                        {
                            inJsonBlock = false;
                            // Try to parse the complete JSON block
                            string jsonContent = jsonBuilder.ToString().Trim();
                            var parsedResponse = TryParseToolCall(jsonContent, toolMap, currentResponse);
                            if (parsedResponse != null)
                            {
                                toolCallResponse = parsedResponse;
                                toolCallDetected = true;
                                break;
                            }
                        }
                        else
                        {
                            jsonBuilder.Append(chunk);
                        }
                    }
                    
                    // Also check for direct JSON outside of code blocks
                    if (currentResponse.TrimStart().StartsWith("{") && currentResponse.TrimEnd().EndsWith("}"))
                    {
                        var parsedResponse = TryParseToolCall(currentResponse, toolMap, currentResponse);
                        if (parsedResponse != null)
                        {
                            toolCallResponse = parsedResponse;
                            toolCallDetected = true;
                            break;
                        }
                    }
                    
                    // Check for JSON pattern in the accumulated response even if it's part of text
                    if (!toolCallDetected && currentResponse.Contains("\"type\""))
                    {
                        // Try to extract any JSON-like object from the accumulated text
                        var jsonPattern = @"(\{(?:[^{}]|(?<Open>\{)|(?<Close-Open>\}))+(?(Open)(?!))\})";
                        var matches = Regex.Matches(currentResponse, jsonPattern, RegexOptions.Singleline);
                        
                        foreach (Match match in matches)
                        {
                            string possibleJson = match.Value;
                            Console.WriteLine($"Found possible JSON in streaming: {possibleJson}");
                            
                            var parsedResponse = TryParseToolCall(possibleJson, toolMap, currentResponse);
                            if (parsedResponse != null)
                            {
                                Console.WriteLine($"Successfully parsed JSON during streaming: {possibleJson}");
                                toolCallResponse = parsedResponse;
                                toolCallDetected = true;
                                break;
                            }
                        }
                        
                        if (toolCallDetected) break;
                    }
                }
                
                // Return the tool call if detected
                if (toolCallDetected && toolCallResponse != null)
                {
                    Console.WriteLine($"Tool call detected early in streaming: {toolCallResponse.Tool}");
                    return toolCallResponse;
                }
                
                // If we reach here, either no tool call was detected or we need to do final parsing
                string result = responseBuilder.ToString();
                Console.WriteLine($"Raw response from LLM: {result}");
                
                // Perform final parsing of the complete response
                string jsonToProcess = result;
                string originalResponse = result;
                
                // Check for markdown code blocks with JSON and extract the content
                if (TryExtractJsonFromMarkdown(result, out string extractedJson))
                {
                    jsonToProcess = extractedJson;
                }
                
                // Process the complete response to look for tool calls
                // ... rest of the existing parsing logic
                
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
                var finalJsonPattern = @"\{\s*""type""\s*:\s*""tool_use"".*?\}"; // Match JSON with tool_use type
                var finalMatch = Regex.Match(result, finalJsonPattern, RegexOptions.Singleline);
                
                if (finalMatch.Success)
                {
                    var json = finalMatch.Value;
                    Console.WriteLine($"Found tool call using regex: {json}");
                    var toolCall = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                    
                    // Extract tool name and parameters
                    if (toolCall.ContainsKey("tool") && toolCall.ContainsKey("tool_input"))
                    {
                        var toolName = toolCall["tool"].ToString();
                        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(
                            toolCall["tool_input"].ToString());
                        
                        // Extract text before the tool call as preamble
                        var preamble = result.Substring(0, finalMatch.Index).Trim();
                        
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
                Console.WriteLine($"Error in streaming tool call detection: {ex.Message}");
                // If parsing fails, treat as regular text
                return new MCPResponse
                {
                    Type = "text",
                    Text = ex.Message,
                    Metadata = new Dictionary<string, object>
                    {
                        { "error", ex.Message }
                    }
                };
            }
        }

        /// <summary>
        /// Helper method to try parsing a string as a tool call
        /// </summary>
        private MCPResponse? TryParseToolCall(string jsonContent, Dictionary<string, ToolDefinition> toolMap, string originalResponse)
        {
            try
            {
                // Basic validation
                if (string.IsNullOrWhiteSpace(jsonContent) || 
                   (!jsonContent.Contains("\"type\"") && !jsonContent.Contains("\"function\"")))
                {
                    return null;
                }
                
                // Log the content we're trying to parse
                Console.WriteLine($"Trying to parse potential tool call: {jsonContent}");
                
                // Try to extract JSON if the content contains text before or after the JSON
                if (!jsonContent.TrimStart().StartsWith("{") || !jsonContent.TrimEnd().EndsWith("}"))
                {
                    // Try to find JSON using regex
                    var jsonPattern = @"(\{(?:[^{}]|(?<Open>\{)|(?<Close-Open>\}))+(?(Open)(?!))\})";
                    var match = Regex.Match(jsonContent, jsonPattern, RegexOptions.Singleline);
                    if (match.Success)
                    {
                        jsonContent = match.Value;
                        Console.WriteLine($"Extracted JSON from text: {jsonContent}");
                    }
                }
                
                // Try to deserialize the JSON (handles newlines and formatting)
                Dictionary<string, object>? parsedJson;
                try
                {
                    parsedJson = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"JSON deserialization failed, trying to normalize: {ex.Message}");
                    
                    // Try to normalize the JSON by removing newlines and extra whitespace
                    var normalized = Regex.Replace(jsonContent, @"\s+", " ").Trim();
                    parsedJson = JsonSerializer.Deserialize<Dictionary<string, object>>(normalized);
                }
                
                if (parsedJson == null) return null;
                
                // Format 1: {"type": "tool_use", "tool": "tool_name", "tool_input": {...}}
                if (parsedJson.ContainsKey("type") && 
                    parsedJson["type"].ToString() == "tool_use" &&
                    parsedJson.ContainsKey("tool") &&
                    parsedJson.ContainsKey("tool_input"))
                {
                    var toolName = parsedJson["tool"].ToString();
                    var parameters = ExtractToolInput(parsedJson["tool_input"]);
                    Console.WriteLine($"Detected standard tool call format: {toolName}");
                    return CreateToolResponse(toolName, parameters, "", originalResponse, toolMap);
                }
                
                // Format 2: {"type": "tool_name", "tool_input": {...}}
                else if (parsedJson.ContainsKey("type") && 
                         parsedJson.ContainsKey("tool_input"))
                {
                    // Important: We're removing the toolMap.ContainsKey check to support tools like list_tools
                    // that might not be explicitly registered in the tool registry
                    var toolName = parsedJson["type"].ToString();
                    Console.WriteLine($"Detected tool call with direct type: {toolName}");
                    var parameters = ExtractToolInput(parsedJson["tool_input"]);
                    return CreateToolResponse(toolName, parameters, "", originalResponse, toolMap);
                }
                
                // Format 3: {"function": "tool_name", "parameters": {...}}
                else if (parsedJson.ContainsKey("function") && 
                         parsedJson.ContainsKey("parameters"))
                {
                    // Also removing toolMap check here
                    var toolName = parsedJson["function"].ToString();
                    Console.WriteLine($"Detected function call format: {toolName}");
                    var parameters = ExtractToolInput(parsedJson["parameters"]);
                    return CreateToolResponse(toolName, parameters, "", originalResponse, toolMap);
                }
                
                // Format 4: Direct tool name as type with no tool_input
                else if (parsedJson.ContainsKey("type") && 
                        !parsedJson.ContainsKey("tool"))
                {
                    var toolName = parsedJson["type"].ToString();
                    Console.WriteLine($"Detected direct tool name as type: {toolName}");
                    
                    // Create empty parameters if not provided
                    Dictionary<string, object> parameters = new Dictionary<string, object>();
                    if (parsedJson.ContainsKey("tool_input"))
                    {
                        parameters = ExtractToolInput(parsedJson["tool_input"]);
                    }
                    
                    return CreateToolResponse(toolName, parameters, "", originalResponse, toolMap);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing potential tool call: {ex.Message}");
            }
            
            return null;
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

            // Use settings from the user preferences instead of hardcoded values
            var requestParams = new Dictionary<string, object>
            {
                { "temperature", Properties.Settings.Default.Temperature },
                { "top_p", Properties.Settings.Default.TopP },
                { "frequency_penalty", Properties.Settings.Default.FrequencyPenalty },
                { "presence_penalty", Properties.Settings.Default.PresencePenalty },
                // Add max context length and max response length
                { "num_ctx", Properties.Settings.Default.MaxContextLength },
                { "num_predict", Properties.Settings.Default.MaxResponseLength }
            };
            
            // Use streaming to detect tool calls while generating the response
            var responseBuilder = new StringBuilder();
            var jsonBuilder = new StringBuilder();
            bool inJsonBlock = false;
            bool toolCallDetected = false;
            MCPResponse toolCallResponse = null;
            
            // Stream the response and look for tool calls on-the-fly
            await foreach (var chunk in _ollamaClient.GenerateStreamResponseWithParamsAsync(prompt, model, requestParams))
            {
                responseBuilder.Append(chunk);
                string currentResponse = responseBuilder.ToString();
                
                // Look for JSON blocks in markdown format
                if (chunk.Contains("```json") || chunk.Contains("```") && !inJsonBlock)
                {
                    inJsonBlock = true;
                    jsonBuilder.Clear();
                    continue;
                }
                
                if (inJsonBlock)
                {
                    if (chunk.Contains("```"))
                    {
                        inJsonBlock = false;
                        // Try to parse the complete JSON block
                        string jsonContent = jsonBuilder.ToString().Trim();
                        toolCallResponse = TryParseToolCall(jsonContent, 
                            _toolRegistry.GetTools().ToDictionary(t => t.Name, t => t), 
                            currentResponse);
                        if (toolCallResponse != null)
                        {
                            toolCallDetected = true;
                            // Add metadata about previous tool call
                            toolCallResponse.Metadata["previous_tool"] = toolName;
                            toolCallResponse.Metadata["previous_result"] = toolResult;
                            toolCallResponse.Metadata["raw_response"] = currentResponse;
                            break;
                        }
                    }
                    else
                    {
                        jsonBuilder.Append(chunk);
                    }
                }
                
                // Also check for direct JSON outside of code blocks
                if (currentResponse.TrimStart().StartsWith("{") && currentResponse.TrimEnd().EndsWith("}"))
                {
                    toolCallResponse = TryParseToolCall(currentResponse, 
                        _toolRegistry.GetTools().ToDictionary(t => t.Name, t => t), 
                        currentResponse);
                    if (toolCallResponse != null)
                    {
                        toolCallDetected = true;
                        // Add metadata about previous tool call
                        toolCallResponse.Metadata["previous_tool"] = toolName;
                        toolCallResponse.Metadata["previous_result"] = toolResult;
                        toolCallResponse.Metadata["raw_response"] = currentResponse;
                        break;
                    }
                }
                
                // Check for regex pattern match in the accumulated response
                if (!toolCallDetected && currentResponse.Contains("\"type\""))
                {
                    var jsonPattern = @"\{\s*""type""\s*:\s*""[^""]+"".*?\}"; // Match JSON with type field
                    var match = Regex.Match(currentResponse, jsonPattern, RegexOptions.Singleline);
                    
                    if (match.Success)
                    {
                        var json = match.Value;
                        toolCallResponse = TryParseToolCall(json, 
                            _toolRegistry.GetTools().ToDictionary(t => t.Name, t => t), 
                            currentResponse);
                        if (toolCallResponse != null)
                        {
                            toolCallDetected = true;
                            // Add metadata about previous tool call and extraction
                            toolCallResponse.Metadata["previous_tool"] = toolName;
                            toolCallResponse.Metadata["previous_result"] = toolResult;
                            toolCallResponse.Metadata["raw_response"] = currentResponse;
                            toolCallResponse.Metadata["extracted_from"] = "regex";
                            // Extract text before the tool call as preamble if not already set
                            if (string.IsNullOrEmpty(toolCallResponse.Text))
                            {
                                toolCallResponse.Text = currentResponse.Substring(0, match.Index).Trim();
                            }
                            break;
                        }
                    }
                }
            }
            
            // Return the tool call if detected during streaming
            if (toolCallDetected && toolCallResponse != null)
            {
                Console.WriteLine($"Tool call detected early in continuation: {toolCallResponse.Tool}");
                return toolCallResponse;
            }
            
            // If we reach here, do final parsing on the complete response
            string result = responseBuilder.ToString();
            
            // Fall back to the original parsing logic for the complete response
            // ... (existing parsing logic)
            
            // No additional tool call, just return the text response
            return new MCPResponse
            {
                Type = "text",
                Text = result
            };
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

            // Generate response with user settings
            var requestParams = new Dictionary<string, object>
            {
                { "temperature", Properties.Settings.Default.Temperature },
                { "top_p", Properties.Settings.Default.TopP },
                { "frequency_penalty", Properties.Settings.Default.FrequencyPenalty },
                { "presence_penalty", Properties.Settings.Default.PresencePenalty },
                { "num_ctx", Properties.Settings.Default.MaxContextLength },
                { "num_predict", Properties.Settings.Default.MaxResponseLength }
            };
            
            return await _ollamaClient.GenerateTextResponseWithParamsAsync(fullPrompt, model, requestParams);
        }
        
        /// <summary>
        /// Executes a tool, whether it's local or on an MCP server
        /// </summary>
        public async Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters, string? serverName = null)
        {
            Console.WriteLine($"Executing tool '{toolName}' on server '{serverName ?? "local"}'");
            
            // If a server name is provided, route to that server
            if (!string.IsNullOrEmpty(serverName) && _serverClients.TryGetValue(serverName, out var serverClient))
            {
                Console.WriteLine($"[MCP] Executing tool '{toolName}' on server '{serverName}'");
                return await serverClient.ExecuteToolAsync(toolName, parameters);
            }
            
            // Check if this is a filesystem tool and we have a FileServer client
            var isFileSystemTool = new[] { "read_file", "write_file", "list_directory", "directory_tree", 
                                          "create_directory", "move_file", "search_files", "get_file_info", 
                                          "list_allowed_directories", "read_multiple_files", "edit_file" }
                                        .Contains(toolName);
                                        
            if (isFileSystemTool && _serverClients.TryGetValue("FileServer", out var fileServerClient))
            {
                Console.WriteLine($"[MCP] Executing tool '{toolName}' on FileServer");
                return await fileServerClient.ExecuteToolAsync(toolName, parameters);
            }
            
            // Otherwise use the local tool registry
            var handler = _toolRegistry.GetToolHandler(toolName);
            if (handler != null)
            {
                Console.WriteLine($"[MCP] Executing tool '{toolName}' using local handler");
                return await handler(parameters);
            }
            
            throw new Exception($"Tool '{toolName}' not found");
        }
        
        // ILLMClient interface implementation - forwarding to the underlying client
        public Task<List<string>> GetAvailableModelsAsync()
        {
            return _ollamaClient.GetAvailableModelsAsync();
        }
        
        public Task<string> GenerateTextResponseAsync(string prompt, string? model = null)
        {
            return _ollamaClient.GenerateTextResponseAsync(prompt, model);
        }
        
        public IAsyncEnumerable<string> GenerateStreamResponseAsync(string prompt, string? model = null)
        {
            return _ollamaClient.GenerateStreamResponseAsync(prompt, model);
        }
        
        public Task<string> GenerateResponseWithImageAsync(string prompt, string imagePath, string? model = null)
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
            
            builder.AppendLine("# Available Tools\n");
            
            foreach (var tool in tools)
            {
                builder.AppendLine($"## {tool.Name}");
                builder.AppendLine($"Description: {tool.Description}\n");
                
                // Format input parameters from Schema if available
                if (!string.IsNullOrEmpty(tool.Schema))
                {
                    try 
                    {
                        var schema = JsonSerializer.Deserialize<Dictionary<string, object>>(tool.Schema);
                        if (schema != null && schema.ContainsKey("properties"))
                        {
                            var properties = JsonSerializer.Deserialize<Dictionary<string, object>>(schema["properties"].ToString());
                            if (properties != null && properties.Count > 0)
                            {
                                builder.AppendLine("Parameters:");
                                foreach (var prop in properties)
                                {
                                    var propDetails = JsonSerializer.Deserialize<Dictionary<string, object>>(prop.Value.ToString());
                                    var description = propDetails.ContainsKey("description") ? propDetails["description"].ToString() : "";
                                    var type = propDetails.ContainsKey("type") ? propDetails["type"].ToString() : "string";
                                    
                                    builder.AppendLine($"- {prop.Key} ({type}): {description}");
                                }
                            }
                            else
                            {
                                builder.AppendLine("No parameters required.");
                            }
                        }
                    }
                    catch
                    {
                        // Could not parse schema, use the input property if available
                        if (tool.Input != null)
                        {
                            builder.AppendLine("Parameters:");
                            FormatToolInput(builder, tool.Input);
                        }
                    }
                }
                // Use Input if Schema is not available
                else if (tool.Input != null)
                {
                    builder.AppendLine("Parameters:");
                    FormatToolInput(builder, tool.Input);
                }
                else
                {
                    builder.AppendLine("No parameters required.");
                }
                
                // Add usage example
                builder.AppendLine("\nExample:");
                builder.AppendLine("```json");
                builder.AppendLine($"{{\n  \"type\": \"{tool.Name}\",");
                builder.AppendLine("  \"tool_input\": {\n    // Parameters here\n  }\n}");
                builder.AppendLine("```\n");
                
                builder.AppendLine("-----");
            }
            
            return builder.ToString();
        }
        
        /// <summary>
        /// Format tool input for the tool description
        /// </summary>
        private void FormatToolInput(StringBuilder builder, Dictionary<string, object> input)
        {
            if (input.ContainsKey("properties"))
            {
                var properties = input["properties"] as Dictionary<string, object>;
                var required = input.ContainsKey("required") 
                    ? (input["required"] as List<string>) ?? new List<string>() 
                    : new List<string>();
                
                if (properties != null)
                {
                    foreach (var param in properties)
                    {
                        var paramName = param.Key;
                        var paramDetails = param.Value as Dictionary<string, object>;
                        var isRequired = required.Contains(paramName) ? "required" : "optional";
                        
                        if (paramDetails != null && paramDetails.ContainsKey("description"))
                        {
                            builder.AppendLine($"- {paramName} ({isRequired}): {paramDetails["description"]}");
                        }
                        else
                        {
                            builder.AppendLine($"- {paramName} ({isRequired})");
                        }
                    }
                }
            }
        }

        public Task<string> GenerateWithFunctionsAsync(string prompt, string model, List<API_Clients.FunctionDefinition> functions)
        {
            throw new NotImplementedException();
        }
    }
}