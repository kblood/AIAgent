using AIAgentTest.Services.Interfaces;
using AIAgentTest.Services.MCP;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIAgentTest.Services
{
    public class MessageParsingService : IMessageParsingService
    {
        public event EventHandler<CodeExtractedEventArgs> CodeExtracted;

        public void ProcessMessage(string message, Action<string> textCallback, Action<string, string> codeCallback)
        {
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
                
                // Alternate format - try to find function call in JSON 
                var jsonMatch = Regex.Match(text, @"\{[\s\S]*?""name""[\s\S]*?:[\s\S]*?""(.*?)""[\s\S]*?""arguments""[\s\S]*?:[\s\S]*?\{([\s\S]*?)\}[\s\S]*?\}");
                if (jsonMatch.Success)
                {
                    var functionName = jsonMatch.Groups[1].Value;
                    var argumentsJson = $"{{{jsonMatch.Groups[2].Value}}}";
                    
                    try
                    {
                        var arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson);
                        var textBeforeCall = text.Substring(0, jsonMatch.Index).Trim();
                        
                        return (functionName, arguments, textBeforeCall);
                    }
                    catch
                    {
                        // JSON parsing failed
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting tool call: {ex.Message}");
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