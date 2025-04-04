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
            _messages.Add(new MCPContextMessage
            {
                Role = role.ToLowerInvariant(),
                Content = content,
                Type = "text" // Explicitly set message type
            });
        }
        
        /// <summary>
        /// Add tool use to context
        /// </summary>
        /// <param name="toolName">Tool name</param>
        /// <param name="toolInput">Tool input</param>
        public void AddToolUse(string toolName, object toolInput)
        {
            _messages.Add(new MCPContextMessage
            {
                Role = "tool",
                ToolName = toolName,
                ToolInput = toolInput,
                Type = "tool_use" // Explicitly set message type 
            });
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
                lastToolUse.ToolResult = success ? toolResult : new { error = errorMessage };
                lastToolUse.Result = success ? toolResult : new { error = errorMessage };
                lastToolUse.Success = success;
                lastToolUse.Error = success ? null : errorMessage;
                lastToolUse.Type = "tool_result";
            }
        }
        
        /// <summary>
        /// Clear context
        /// </summary>
        public void ClearContext()
        {
            _messages.Clear();
            _summarizedContext = null;
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
                    
                    // Remove old messages
                    _messages.RemoveRange(0, oldMessages.Count);
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
    
        /// <summary>
        /// Get full context
        /// </summary>
        /// <returns>Full context</returns>
        public string GetFullContext()
        {
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("=== FULL CURRENT CONTEXT ===");
            contextBuilder.AppendLine();
            
            if (!string.IsNullOrEmpty(_summarizedContext))
            {
                contextBuilder.AppendLine("=== SUMMARIZED HISTORY ===");
                contextBuilder.AppendLine(_summarizedContext);
                contextBuilder.AppendLine();
                contextBuilder.AppendLine("=== RECENT MESSAGES ===");
            }
            
            if (_messages.Count == 0)
            {
                contextBuilder.AppendLine("(Context is empty)");
                return contextBuilder.ToString();
            }
            
            foreach (var msg in _messages)
            {
                if (msg.Role == "tool" || (msg.Type != null && (msg.Type == "tool_use" || msg.Type == "tool_result")))
                {
                    contextBuilder.AppendLine($"--- Tool: {msg.ToolName} ---");
                    contextBuilder.AppendLine($"Input: {Serialize(msg.ToolInput)}");
                    
                    if (msg.ToolResult != null)
                    {
                        contextBuilder.AppendLine($"Result: {Serialize(msg.ToolResult)}");
                    }
                    else
                    {
                        contextBuilder.AppendLine("Result: [pending]");
                    }
                    contextBuilder.AppendLine();
                }
                else
                {
                    contextBuilder.AppendLine($"{msg.Role}: {msg.Content}");
                    contextBuilder.AppendLine();
                }
            }
            
            return contextBuilder.ToString();
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