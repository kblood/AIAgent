using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AIAgentTest.API_Clients;
using AIAgentTest.Services.Interfaces;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// MCP-aware context manager
    /// </summary>
    public class MCPContextManager : IMCPContextManager
    {
        private readonly OllamaClient _ollamaClient;
        private readonly List<MCPContextMessage> _messages = new List<MCPContextMessage>();
        private string _summarizedContext;
        
        // Token tracking
        private int _totalContextTokens = 0;
        private int _largestResponseTokens = 0;
        private Dictionary<string, int> _messageTokenCounts = new Dictionary<string, int>();
        
        /// <summary>
        /// Whether context is enabled
        /// </summary>
        public bool IsContextEnabled { get; set; } = true;
    
        /// <summary>
        /// Default model for summarization
        /// </summary>
        public string DefaultModel { get; set; } = "llama3";
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ollamaClient">Ollama client for summarization</param>
        public MCPContextManager(OllamaClient ollamaClient)
        {
            _ollamaClient = ollamaClient ?? throw new ArgumentNullException(nameof(ollamaClient));
        }
        
        /// <summary>
        /// Get contextual prompt
        /// </summary>
        /// <param name="input">User input</param>
        /// <returns>Prompt with context</returns>
        public string GetContextualPrompt(string input)
        {
            if (!IsContextEnabled)
                return input;
            
            var contextBuilder = new StringBuilder();
            
            if (!string.IsNullOrEmpty(_summarizedContext))
                contextBuilder.AppendLine($"Previous context summary: {_summarizedContext}");
            
            var recentMessages = _messages
                .TakeLast(5)
                .ToList();
            
            foreach (var msg in recentMessages)
            {
                if (msg.Role == "tool" || (msg.Type != null && (msg.Type == "tool_use" || msg.Type == "tool_result")))
                {
                    // More detailed tool information
                    contextBuilder.AppendLine($"[Tool call: {msg.ToolName}]");
                    contextBuilder.AppendLine($"Tool Input: {Serialize(msg.ToolInput)}");
                    
                    if (msg.ToolResult != null)
                    {
                        contextBuilder.AppendLine($"Tool Result: {Serialize(msg.ToolResult)}");
                    }
                    contextBuilder.AppendLine(""); // Empty line for readability
                }
                else
                {
                    contextBuilder.AppendLine($"{msg.Role}: {msg.Content}");
                }
            }
            
            contextBuilder.AppendLine($"User: {input}");
            
            return contextBuilder.ToString();
        }
        
        /// <summary>
        /// Get MCP contextual prompt
        /// </summary>
        /// <param name="input">User input</param>
        /// <returns>Prompt with MCP context</returns>
        public string GetMCPContextualPrompt(string input)
        {
            if (!IsContextEnabled)
                return input;
            
            var contextBuilder = new StringBuilder();
            
            if (!string.IsNullOrEmpty(_summarizedContext))
                contextBuilder.AppendLine($"Previous context summary: {_summarizedContext}\n");
            
            var recentMessages = _messages
                .TakeLast(7)
                .ToList();
            
            // Add a message to help the model understand the format
            if (recentMessages.Any(m => m.Role == "tool" || (m.Type != null && (m.Type == "tool_use" || m.Type == "tool_result"))))
            {
                contextBuilder.AppendLine("The conversation includes tool calls and results in the following format:");
                contextBuilder.AppendLine("[Tool call: tool_name]\nTool Input: {...}\nTool Result: {...}\n");
            }
            
            foreach (var msg in recentMessages)
            {
                if (msg.Role == "tool" || (msg.Type != null && (msg.Type == "tool_use" || msg.Type == "tool_result")))
                {
                    // Format tool information for better context
                    contextBuilder.AppendLine($"[Tool call: {msg.ToolName}]");
                    contextBuilder.AppendLine($"Tool Input: {Serialize(msg.ToolInput)}");
                    
                    if (msg.ToolResult != null)
                    {
                        contextBuilder.AppendLine($"Tool Result: {Serialize(msg.ToolResult)}");
                    }
                    contextBuilder.AppendLine(""); // Empty line for readability
                }
                else
                {
                    contextBuilder.AppendLine($"{msg.Role}: {msg.Content}\n");
                }
            }
            
            contextBuilder.AppendLine($"User: {input}");
            
            return contextBuilder.ToString();
        }
        
        /// <summary>
        /// Add message to context
        /// </summary>
        /// <param name="role">Message role</param>
        /// <param name="content">Message content</param>
        public void AddMessage(string role, string content)
        {
            var message = new MCPContextMessage
            {
                Role = role.ToLowerInvariant(),
                Content = content,
                Type = "text", // Explicitly set message type
                Metadata = new Dictionary<string, object>()
            };
            
            _messages.Add(message);
            
            // Track token count
            int tokenCount = TokenCounterUtility.EstimateTokenCount(content);
            _totalContextTokens += tokenCount;
            
            // Store token count for this message
            string messageId = Guid.NewGuid().ToString();
            _messageTokenCounts[messageId] = tokenCount;
            message.Metadata["MessageId"] = messageId;
            message.Metadata["TokenCount"] = tokenCount;
            
            // Update largest response if this is a model response
            if (!role.Equals("user", StringComparison.OrdinalIgnoreCase) && 
                !role.Equals("system", StringComparison.OrdinalIgnoreCase))
            {
                _largestResponseTokens = Math.Max(_largestResponseTokens, tokenCount);
            }
        }
        
        /// <summary>
        /// Add tool use to context
        /// </summary>
        /// <param name="toolName">Tool name</param>
        /// <param name="toolInput">Tool input</param>
        public void AddToolUse(string toolName, object toolInput)
        {
            var message = new MCPContextMessage
            {
                Role = "tool",
                ToolName = toolName,
                ToolInput = toolInput,
                Type = "tool_use", // Explicitly set message type
                Metadata = new Dictionary<string, object>()
            };
            
            _messages.Add(message);
            
            // Track token counts
            string toolInputJson = Serialize(toolInput);
            int tokenCount = TokenCounterUtility.EstimateJsonTokenCount(toolInputJson);
            _totalContextTokens += tokenCount;
            
            // Store token count
            string messageId = Guid.NewGuid().ToString();
            _messageTokenCounts[messageId] = tokenCount;
            message.Metadata["MessageId"] = messageId;
            message.Metadata["TokenCount"] = tokenCount;
        }
        
        /// <summary>
        /// Add tool result to context
        /// </summary>
        /// <param name="toolName">Tool name</param>
        /// <param name="toolResult">Tool result</param>
        /// <param name="success">Whether the tool succeeded</param>
        /// <param name="errorMessage">Error message if the tool failed</param>
        public void AddToolResult(string toolName, object toolResult, bool success, string errorMessage = null)
        {
            // Find the last tool use
            var lastToolUse = _messages
                .LastOrDefault(m => (m.Role == "tool" || (m.Type != null && m.Type == "tool_use")) && 
                               m.ToolName == toolName && 
                               m.ToolResult == null && 
                               m.Result == null);
            
            if (lastToolUse != null)
            {
                object result = success ? toolResult : new { error = errorMessage };
                lastToolUse.ToolResult = result;
                lastToolUse.Result = result;
                lastToolUse.Success = success;
                lastToolUse.Error = success ? null : errorMessage;
                lastToolUse.Type = "tool_result";
                
                // Track token counts
                if (result != null)
                {
                    string resultJson = Serialize(result);
                    int tokenCount = TokenCounterUtility.EstimateJsonTokenCount(resultJson);
                    _totalContextTokens += tokenCount;
                    
                    // Store token count
                    if (lastToolUse.Metadata != null)
                    {
                        lastToolUse.Metadata["ResultTokenCount"] = tokenCount;
                    }
                }
            }
        }
        
        /// <summary>
        /// Clear context
        /// </summary>
        public void ClearContext()
        {
            _messages.Clear();
            _summarizedContext = null;
            
            // Reset token tracking
            _totalContextTokens = 0;
            _largestResponseTokens = 0;
            _messageTokenCounts.Clear();
        }
        
        /// <summary>
        /// Summarize context
        /// </summary>
        /// <param name="model">Model to use for summarization</param>
        public async Task SummarizeContext(string model)
        {
            if (_messages.Count <= 5)
            {
                // Not enough messages to summarize
                return;
            }
            
            try
            {
                // Create a summarization prompt
                var prompt = "Create a concise summary of the following conversation that includes" +
                             " all important details. The summary will be used as context for continuing" +
                             " the conversation, so make sure to include all relevant points:\n\n";
                
                // Add messages to summarize
                var oldMessages = _messages
                    .Take(_messages.Count - 5)
                    .ToList();
                
                foreach (var msg in oldMessages)
                {
                    if (msg.Role == "tool" || (msg.Type != null && (msg.Type == "tool_use" || msg.Type == "tool_result")))
                    {
                        prompt += $"[Tool: {msg.ToolName}]\n";
                        prompt += $"Input: {Serialize(msg.ToolInput)}\n";
                        
                        if (msg.ToolResult != null)
                        {
                            prompt += $"Result: {Serialize(msg.ToolResult)}\n";
                        }
                        prompt += "\n";
                    }
                    else
                    {
                        prompt += $"{msg.Role}: {msg.Content}\n\n";
                    }
                }
                
                // Generate summary using Ollama
                var response = await _ollamaClient.GenerateTextAsync(prompt, model);
                
                if (!string.IsNullOrEmpty(response))
                {
                    _summarizedContext = response;
                    
                    // Update token tracking before removing old messages
                    foreach (var msg in oldMessages)
                    {
                        if (msg.Metadata != null && msg.Metadata.ContainsKey("MessageId"))
                        {
                            string messageId = msg.Metadata["MessageId"].ToString();
                            if (_messageTokenCounts.ContainsKey(messageId))
                            {
                                _totalContextTokens -= _messageTokenCounts[messageId];
                                _messageTokenCounts.Remove(messageId);
                            }
                        }
                    }
                    
                    // Remove old messages
                    _messages.RemoveRange(0, oldMessages.Count);
                    
                    // Add the summary tokens
                    int summaryTokens = TokenCounterUtility.EstimateTokenCount(response);
                    _totalContextTokens += summaryTokens;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error summarizing context: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get summary of context
        /// </summary>
        /// <returns>Context summary</returns>
        public string Summarize()
        {
            return _summarizedContext ?? string.Empty;
        }
    
        public string GetFullContext()
        {
            // Force refresh before showing content
            RefreshContext();
            
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("=== FULL CURRENT CONTEXT ===\n");
            
            if (!string.IsNullOrEmpty(_summarizedContext))
            {
                contextBuilder.AppendLine("=== SUMMARIZED HISTORY ===");
                contextBuilder.AppendLine(_summarizedContext);
                contextBuilder.AppendLine("\n=== RECENT MESSAGES ===");
            }
            
            if (_messages.Count == 0)
            {
                contextBuilder.AppendLine("(Context is empty)");
                return contextBuilder.ToString();
            }
            
            bool inToolSection = false;
            string currentToolName = null;
            
            // Analyze the messages to find tool sequences
            for (int i = 0; i < _messages.Count; i++)
            {
                var msg = _messages[i];
                
                // Always display the basic message
                contextBuilder.AppendLine($"{msg.Role}: {msg.Content}");
                
                // Check if this is a tool-related message
                if (msg.Role == "tool" || 
                    (msg.Type != null && (msg.Type == "tool_use" || msg.Type == "tool_result")) || 
                    (msg.ToolName != null && !string.IsNullOrEmpty(msg.ToolName)))
                {
                    // This is a tool message - display all details
                    inToolSection = true;
                    currentToolName = msg.ToolName;
                    
                    contextBuilder.AppendLine($"--- TOOL DETAILS: {msg.ToolName} ---");
                    
                    if (msg.ToolInput != null)
                    {
                        contextBuilder.AppendLine($"Tool Input: {Serialize(msg.ToolInput)}");
                    }
                    
                    if (msg.ToolResult != null)
                    {
                        contextBuilder.AppendLine($"Tool Result: {Serialize(msg.ToolResult)}");
                    }
                }
                // Or if this is a system message with "Tool result"
                else if (msg.Role == "system" && msg.Content != null && msg.Content.Contains("Tool result"))
                {
                    inToolSection = true;
                    
                    // Extract tool name from the message
                    string toolName = msg.Content.Replace("Tool result from ", "").Trim();
                    currentToolName = toolName;
                    
                    // Look ahead for potential JSON result
                    for (int j = i + 1; j < Math.Min(_messages.Count, i + 3); j++)
                    {
                        var nextMsg = _messages[j];
                        if (nextMsg.Content != null && 
                            (nextMsg.Content.StartsWith("{") || nextMsg.Content.StartsWith("[")))
                        {
                            // This looks like a JSON result
                            contextBuilder.AppendLine($"--- TOOL RESULT: {toolName} ---");
                            contextBuilder.AppendLine(nextMsg.Content);
                            break;
                        }
                    }
                }
                // Otherwise, if we were in a tool section and now moved to another message type
                else if (inToolSection)
                {
                    inToolSection = false;
                    currentToolName = null;
                }
                
                // Add a blank line after each message for readability
                contextBuilder.AppendLine();
            }
            
            // Add token statistics
            contextBuilder.AppendLine(GetTokenStatistics());
            
            return contextBuilder.ToString();
        }
        
        /// <summary>
        /// Refreshes the context (rebuilds the tool calls/results)
        /// </summary>
        public void RefreshContext()
        {
            // Rebuild/reprocess all tool calls/results to ensure they're properly represented
            // This is needed when loading chat sessions
            for (int i = 0; i < _messages.Count; i++)
            {
                var message = _messages[i];
                
                // If this is a tool call without a proper ToolName or ToolInput, try to repair it
                if (message.Role == "tool" && (string.IsNullOrEmpty(message.ToolName) || message.ToolInput == null))
                {
                    // Attempt to reconstruct based on content
                    if (!string.IsNullOrEmpty(message.Content))
                    {
                        // Extract tool name from content
                        if (message.Content.Contains("Using") && message.Content.Contains("tool"))
                        {
                            var parts = message.Content.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int j = 0; j < parts.Length; j++)
                            {
                                if (parts[j] == "tool")
                                {
                                    if (j > 0)
                                    {
                                        message.ToolName = parts[j - 1]; // Name might be before "tool"
                                        message.Type = "tool_use";
                                    }
                                    else if (j < parts.Length - 1)
                                    {
                                        message.ToolName = parts[j + 1]; // Or after "tool"
                                        message.Type = "tool_use";
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                
                // If the next message is a system message with tool result, link them
                if (message.Type == "tool_use" && i < _messages.Count - 1)
                {
                    var nextMessage = _messages[i + 1];
                    if (nextMessage.Role == "system" && 
                        nextMessage.Content != null && 
                        nextMessage.Content.Contains("Tool result"))
                    {
                        // Try to parse the tool result content
                        var resultContent = nextMessage.Content.Replace("Tool result from ", "").Trim();
                        
                        // Look for the result in one of the next few messages
                        for (int j = i + 2; j < Math.Min(_messages.Count, i + 5); j++)
                        {
                            var potentialResultMessage = _messages[j];
                            if (potentialResultMessage.Content != null && potentialResultMessage.Content.StartsWith("{"))
                            {
                                // This looks like a JSON result
                                try
                                {
                                    message.ToolResult = System.Text.Json.JsonSerializer.Deserialize<object>(potentialResultMessage.Content);
                                    message.Result = message.ToolResult;
                                    message.Success = true;
                                    break;
                                }
                                catch
                                {
                                    // Not valid JSON, continue looking
                                }
                            }
                        }
                    }
                }
            }
            
            // Recalculate token counts if needed
            RecalculateTokenCounts();
        }
        
        /// <summary>
        /// Recalculate all token counts
        /// </summary>
        private void RecalculateTokenCounts()
        {
            _totalContextTokens = 0;
            _largestResponseTokens = 0;
            _messageTokenCounts.Clear();
            
            // Count summarized context
            if (!string.IsNullOrEmpty(_summarizedContext))
            {
                _totalContextTokens += TokenCounterUtility.EstimateTokenCount(_summarizedContext);
            }
            
            // Count each message
            foreach (var msg in _messages)
            {
                int messageTokens = 0;
                
                // Count message content
                if (!string.IsNullOrEmpty(msg.Content))
                {
                    int contentTokens = TokenCounterUtility.EstimateTokenCount(msg.Content);
                    messageTokens += contentTokens;
                }
                
                // Count tool inputs/outputs
                if (msg.ToolInput != null)
                {
                    int inputTokens = TokenCounterUtility.EstimateJsonTokenCount(Serialize(msg.ToolInput));
                    messageTokens += inputTokens;
                }
                
                if (msg.ToolResult != null)
                {
                    int resultTokens = TokenCounterUtility.EstimateJsonTokenCount(Serialize(msg.ToolResult));
                    messageTokens += resultTokens;
                }
                
                // Store token count
                string messageId = Guid.NewGuid().ToString();
                if (msg.Metadata == null)
                {
                    msg.Metadata = new Dictionary<string, object>();
                }
                msg.Metadata["MessageId"] = messageId;
                msg.Metadata["TokenCount"] = messageTokens;
                _messageTokenCounts[messageId] = messageTokens;
                
                // Add to total
                _totalContextTokens += messageTokens;
                
                // Update largest response if this is a model response
                if (!msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase) && 
                    !msg.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    _largestResponseTokens = Math.Max(_largestResponseTokens, messageTokens);
                }
            }
        }
        
        /// <summary>
        /// Get token statistics
        /// </summary>
        /// <returns>Token statistics as a formatted string</returns>
        public string GetTokenStatistics()
        {
            var builder = new StringBuilder();
            builder.AppendLine("=== TOKEN STATISTICS ===");
            builder.AppendLine($"Total Context Tokens: {_totalContextTokens}");
            builder.AppendLine($"Largest Response Tokens: {_largestResponseTokens}");
            
            // Calculate current context size
            int currentContextSize = 0;
            if (!string.IsNullOrEmpty(_summarizedContext))
            {
                currentContextSize += TokenCounterUtility.EstimateTokenCount(_summarizedContext);
            }
            
            foreach (var msg in _messages)
            {
                if (msg.Metadata != null && msg.Metadata.ContainsKey("TokenCount"))
                {
                    currentContextSize += (int)msg.Metadata["TokenCount"];
                }
                else if (!string.IsNullOrEmpty(msg.Content))
                {
                    currentContextSize += TokenCounterUtility.EstimateTokenCount(msg.Content);
                }
            }
            
            builder.AppendLine($"Current Context Size: {currentContextSize} tokens");
            
            return builder.ToString();
        }
        
        /// <summary>
        /// Get information about tool usage in the current context
        /// </summary>
        /// <returns>Tool usage information as a formatted string</returns>
        public string GetToolUsageInfo()
        {
            var stringBuilder = new StringBuilder();
            
            // If there are no messages, return empty string
            if (_messages.Count == 0)
            {
                return string.Empty;
            }
            
            // Collect tool usage stats
            var toolCounts = new Dictionary<string, int>();
            var toolSuccess = new Dictionary<string, int>();
            var toolFailure = new Dictionary<string, int>();
            var toolOutputs = new Dictionary<string, List<object>>();
            
            foreach (var msg in _messages)
            {
                if (msg.Role == "tool" || (msg.Type != null && (msg.Type == "tool_use" || msg.Type == "tool_result")))
                {
                    string toolName = msg.ToolName ?? "unknown";
                    
                    // Count tool usage
                    if (!toolCounts.ContainsKey(toolName))
                    {
                        toolCounts[toolName] = 0;
                        toolSuccess[toolName] = 0;
                        toolFailure[toolName] = 0;
                        toolOutputs[toolName] = new List<object>();
                    }
                    
                    toolCounts[toolName]++;
                    
                    // Count success/failure
                    if (msg.ToolResult != null)
                    {
                        if (msg.Success)
                        {
                            toolSuccess[toolName]++;
                            toolOutputs[toolName].Add(msg.ToolResult);
                        }
                        else
                        {
                            toolFailure[toolName]++;
                        }
                    }
                }
                // Also look for system messages with "Tool result"
                else if (msg.Role == "system" && msg.Content != null && msg.Content.Contains("Tool result from"))
                {
                    string toolName = msg.Content.Replace("Tool result from ", "").Trim();
                    
                    if (!toolCounts.ContainsKey(toolName))
                    {
                        toolCounts[toolName] = 0;
                        toolSuccess[toolName] = 0;
                        toolFailure[toolName] = 0;
                        toolOutputs[toolName] = new List<object>();
                    }
                    
                    toolCounts[toolName]++;
                }
            }
            
            // Build output string
            stringBuilder.AppendLine("Tool Usage Statistics:");
            
            if (toolCounts.Count == 0)
            {
                stringBuilder.AppendLine("No tools have been used in this session.");
                return stringBuilder.ToString();
            }
            
            foreach (var toolName in toolCounts.Keys)
            {
                stringBuilder.AppendLine($"\n{toolName}:");
                stringBuilder.AppendLine($"  Total calls: {toolCounts[toolName]}");
                stringBuilder.AppendLine($"  Successful: {toolSuccess[toolName]}");
                stringBuilder.AppendLine($"  Failed: {toolFailure[toolName]}");
                
                // Add example output if available
                if (toolOutputs[toolName].Count > 0)
                {
                    stringBuilder.AppendLine("  Latest output sample:");
                    stringBuilder.AppendLine($"  {Serialize(toolOutputs[toolName].Last())}");
                }
            }
            
            return stringBuilder.ToString();
        }
        
        /// <summary>
        /// Helper method to serialize objects to JSON
        /// </summary>
        private string Serialize(object obj)
        {
            if (obj == null) return "null";
            
            try
            {
                return System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            catch (Exception)
            {
                return obj.ToString();
            }
        }
    
        /// <summary>
        /// Get debug info
        /// </summary>
        /// <returns>Debug info</returns>
        public string GetDebugInfo()
        {
            var info = new StringBuilder();
            
            info.AppendLine($"Context Enabled: {IsContextEnabled}");
            info.AppendLine($"Default Model: {DefaultModel}");
            info.AppendLine($"Messages Count: {_messages.Count}");
            info.AppendLine($"Has Summary: {!string.IsNullOrEmpty(_summarizedContext)}");
            
            // Add token statistics
            info.AppendLine();
            info.AppendLine($"Total Tokens: {_totalContextTokens}");
            info.AppendLine($"Largest Response: {_largestResponseTokens} tokens");
            
            return info.ToString();
        }
    
        /// <summary>
        /// Get timestamp of last message
        /// </summary>
        /// <returns>Last message timestamp</returns>
        public DateTime GetLastMessageTimestamp()
        {
            return DateTime.Now; // Placeholder, should store timestamps with messages
        }
    }
}