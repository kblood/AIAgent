using AIAgentTest.Services.Interfaces;
using AIAgentTest.Services.MCP;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace AIAgentTest.Services
{
    public class MessageParsingService : IMessageParsingService
    {
        public event EventHandler<CodeExtractedEventArgs> CodeExtracted;

        public void ProcessMessage(string message, Action<string> textCallback, Action<string, string> codeCallback)
        {
            // First check if this is a tool call or tool result message
            if (IsToolInteractionMessage(message))
            {
                Debug.WriteLine("Processing as tool interaction message");
                ProcessToolInteractionMessage(message, textCallback, codeCallback);
                return;
            }

            // Standard code block extraction
            string pattern = @"```(\w*)\r?\n(.*?)\r?\n```|```(\w*)\s*(.*?)```";
            int lastIndex = 0;

            var matches = Regex.Matches(message, pattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                string textBefore = message.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrWhiteSpace(textBefore))
                {
                    textCallback(textBefore);
                }

                string language = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
                string code = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;

                codeCallback(language, code.Trim());
                OnCodeExtracted(code.Trim(), language);

                lastIndex = match.Index + match.Length;
            }

            string remaining = message.Substring(lastIndex);
            if (!string.IsNullOrWhiteSpace(remaining))
            {
                textCallback(remaining);
            }
        }
        
        // New method to check if a message appears to be a tool interaction
        private bool IsToolInteractionMessage(string message)
        {
            // Check for tool headers
            if (message.Contains("[Using") && message.Contains("tool...]"))
                return true;
                
            if (message.Contains("[Tool result from"))
                return true;
                
            // Check for JSON format that looks like a tool call
            if (message.Contains("\"type\":") && message.Contains("\"tool_input\":"))
                return true;
                
            return false;
        }
        
        // New method to process tool interaction messages
        private void ProcessToolInteractionMessage(string message, Action<string> textCallback, Action<string, string> codeCallback)
        {
            Debug.WriteLine("Processing tool interaction message: " + message.Substring(0, Math.Min(50, message.Length)));
            
            // Try to extract tool call sections
            var toolHeaderMatch = Regex.Match(message, @"\[Using (.*?) tool\.\.\.\]");
            var resultHeaderMatch = Regex.Match(message, @"\[Tool result from (.*?)\]");
            
            // If both parts are found, try to process as a complete tool interaction
            if (toolHeaderMatch.Success && resultHeaderMatch.Success)
            {
                string toolName = toolHeaderMatch.Groups[1].Value;
                Debug.WriteLine($"Found tool interaction for {toolName}");
                
                // Extract the JSON parts - tool call and result
                var jsonMatches = Regex.Matches(message, @"\{.*?\}", RegexOptions.Singleline);
                
                if (jsonMatches.Count >= 2)
                {
                    string toolCallJson = jsonMatches[0].Value;
                    string resultJson = jsonMatches[1].Value;
                    
                    // Format for display
                    string toolHeader = $"[Using {toolName} tool...]\n";
                    string resultHeader = $"[Tool result from {toolName}]\n";
                    string formattedToolCallJson = FormatJson(toolCallJson);
                    string formattedResultJson = FormatJson(resultJson);
                    
                    // Display a simplified message in the chat instead of the raw JSON
                    textCallback($"[Tool interaction - view code for details]\n");
                    
                    // Create unified code block
                    string fullToolInteraction = 
                        toolHeader + formattedToolCallJson + "\n\n" + 
                        resultHeader + formattedResultJson;
                    
                    codeCallback("json", fullToolInteraction);
                    OnCodeExtracted(fullToolInteraction, "json");
                    
                    return;
                }
            }
            
            // If we couldn't parse as a tool interaction, process normally
            Debug.WriteLine("Falling back to normal message processing");
            
            // Standard code block extraction
            string pattern = @"```(\w*)\r?\n(.*?)\r?\n```|```(\w*)\s*(.*?)```";
            int lastIndex = 0;

            var matches = Regex.Matches(message, pattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                string textBefore = message.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrWhiteSpace(textBefore))
                {
                    textCallback(textBefore);
                }

                string language = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
                string code = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;

                codeCallback(language, code.Trim());
                OnCodeExtracted(code.Trim(), language);

                lastIndex = match.Index + match.Length;
            }

            string remaining = message.Substring(lastIndex);
            if (!string.IsNullOrWhiteSpace(remaining))
            {
                textCallback(remaining);
            }
        }
        
        // Helper method to format JSON for display
        private string FormatJson(string json)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var obj = JsonSerializer.Deserialize<object>(json);
                return JsonSerializer.Serialize(obj, options);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error formatting JSON: {ex.Message}");
                // If parsing fails, return the original
                return json;
            }
        }

        public IEnumerable<(string Text, string Language, string Code)> ExtractCodeBlocks(string message)
        {
            string pattern = @"```(\w*)\r?\n(.*?)\r?\n```|```(\w*)\s*(.*?)```";
            int lastIndex = 0;
            var results = new List<(string Text, string Language, string Code)>();

            var matches = Regex.Matches(message, pattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                string textBefore = message.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrWhiteSpace(textBefore))
                {
                    results.Add((textBefore, null, null));
                }

                string language = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
                string code = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;

                results.Add((null, language, code.Trim()));
                lastIndex = match.Index + match.Length;
            }

            string remaining = message.Substring(lastIndex);
            if (!string.IsNullOrWhiteSpace(remaining))
            {
                results.Add((remaining, null, null));
            }

            return results;
        }
        
        /// <summary>
        /// Extracts an MCP response from a text message
        /// </summary>
        public MCPResponse ExtractMCPResponse(string text)
        {
            var (toolName, parameters, textBeforeCall) = ExtractToolCall(text);
            
            if (!string.IsNullOrEmpty(toolName) && parameters != null)
            {
                return new MCPResponse
                {
                    Type = "tool_use",
                    Tool = toolName,
                    Input = parameters,
                    Text = textBeforeCall,
                    Metadata = new Dictionary<string, object>
                    {
                        { "raw_response", text }
                    }
                };
            }
            else
            {
                return new MCPResponse
                {
                    Type = "text",
                    Text = text
                };
            }
        }
        
        /// <summary>
        /// Extracts tool call information from a text message
        /// </summary>
        public (string ToolName, Dictionary<string, object> Parameters, string TextBeforeCall) ExtractToolCall(string text)
        {
            try
            {
                // Look for tool call block
                var match = Regex.Match(text, @"<tool_call>\s*({.*?})\s*</tool_call>", RegexOptions.Singleline);
                
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
                        var textBeforeCall = text.Substring(0, match.Index).Trim();
                        
                        return (toolName, parameters, textBeforeCall);
                    }
                }
                
                // Look for tool call in standard JSON format with type and tool_input
                try
                {
                    // Check if the whole text is a JSON object
                    if (text.TrimStart().StartsWith("{") && text.TrimEnd().EndsWith("}"))
                    {
                        var jsonObject = JsonSerializer.Deserialize<Dictionary<string, object>>(text);
                        
                        // Format 1: {"type": "tool_name", "tool_input": {...}}
                        if (jsonObject.ContainsKey("type") && jsonObject.ContainsKey("tool_input"))
                        {
                            var toolName = jsonObject["type"].ToString();
                            Dictionary<string, object> parameters;
                            
                            // Handle different formats of tool_input
                            if (jsonObject["tool_input"] is JsonElement jsonElement)
                            {
                                parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                    jsonElement.GetRawText());
                            }
                            else
                            {
                                parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(
                                    JsonSerializer.Serialize(jsonObject["tool_input"]));
                            }
                            
                            return (toolName, parameters, ""); // No preamble if it's a full JSON object
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to parse complete JSON format: {ex.Message}");
                }
                
                // Try to extract tool call using regex
                var toolJsonMatch = Regex.Match(text, @"\{\s*""type""\s*:\s*""(.*?)"".*?""tool_input""\s*:\s*(\{.*?\})", RegexOptions.Singleline);
                if (toolJsonMatch.Success)
                {
                    var toolName = toolJsonMatch.Groups[1].Value;
                    var parametersJson = toolJsonMatch.Groups[2].Value;
                    
                    try
                    {
                        var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(parametersJson);
                        var textBeforeCall = text.Substring(0, toolJsonMatch.Index).Trim();
                        
                        return (toolName, parameters, textBeforeCall);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to parse tool_input: {ex.Message}");
                    }
                }
                
                // Alternate format - try to find function call in JSON 
                var functionMatch = Regex.Match(text, @"\{[\s\S]*?""name""[\s\S]*?:[\s\S]*?""(.*?)""[\s\S]*?""arguments""[\s\S]*?:[\s\S]*?\{([\s\S]*?)\}[\s\S]*?\}");
                if (functionMatch.Success)
                {
                    var functionName = functionMatch.Groups[1].Value;
                    var argumentsJson = $"{{{functionMatch.Groups[2].Value}}}";
                    
                    try
                    {
                        var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson);
                        var textBeforeCall = text.Substring(0, functionMatch.Index).Trim();
                        
                        return (functionName, arguments, textBeforeCall);
                    }
                    catch
                    {
                        Debug.WriteLine("Failed to parse function arguments");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting tool call: {ex.Message}");
            }
            
            return (null, null, null);
        }
        
        // Added for testing purposes
        public void OnCodeExtracted(string code, string language)
        {
            CodeExtracted?.Invoke(this, new CodeExtractedEventArgs(code, language));
        }
    }
}