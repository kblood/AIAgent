using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AIAgentTest.API_Clients;
using AIAgentTest.Services.Interfaces;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Implementation of IMCPContextManager that extends ContextManager with MCP capabilities
    /// </summary>
    public class MCPContextManager : ContextManager, IMCPContextManager
    {
        private List<MCPContextMessage> _mcpContextHistory = new List<MCPContextMessage>();
        private readonly int _maxContextSize = 50; // Configurable
        
        /// <summary>
        /// Creates a new MCPContextManager
        /// </summary>
        /// <param name="ollamaClient">Ollama client for summarization</param>
        public MCPContextManager(OllamaClient ollamaClient) : base(ollamaClient)
        {
        }
        
        /// <summary>
        /// Adds a tool use to the context
        /// </summary>
        public void AddToolUse(string toolName, Dictionary<string, object> input)
        {
            _mcpContextHistory.Add(new MCPContextMessage
            {
                Type = "tool_use",
                ToolName = toolName,
                Input = input
            });
            
            EnforceSizeLimit();
        }
        
        /// <summary>
        /// Adds a tool result to the context
        /// </summary>
        public void AddToolResult(string toolName, object result, bool success, string error = null)
        {
            _mcpContextHistory.Add(new MCPContextMessage
            {
                Type = "tool_result",
                ToolName = toolName,
                Result = result,
                Success = success,
                Error = error
            });
            
            EnforceSizeLimit();
        }
        
        /// <summary>
        /// Adds a retrieval request to the context
        /// </summary>
        public void AddRetrievalRequest(string query, string source = null)
        {
            _mcpContextHistory.Add(new MCPContextMessage
            {
                Type = "retrieval_request",
                Input = new Dictionary<string, object> 
                { 
                    { "query", query },
                    { "source", source }
                }
            });
            
            EnforceSizeLimit();
        }
        
        /// <summary>
        /// Adds a retrieval result to the context
        /// </summary>
        public void AddRetrievalResult(string source, object result)
        {
            _mcpContextHistory.Add(new MCPContextMessage
            {
                Type = "retrieval_result",
                Input = new Dictionary<string, object> { { "source", source } },
                Result = result,
                Success = true
            });
            
            EnforceSizeLimit();
        }
        
        /// <summary>
        /// Adds a user interaction to the context
        /// </summary>
        public void AddUserInteraction(string prompt, string response = null)
        {
            _mcpContextHistory.Add(new MCPContextMessage
            {
                Type = "user_interaction",
                Input = new Dictionary<string, object> { { "prompt", prompt } },
                Result = response,
                Success = response != null
            });
            
            EnforceSizeLimit();
        }
        
        /// <summary>
        /// Gets a prompt with MCP context included
        /// </summary>
        public string GetMCPContextualPrompt(string input)
        {
            // Format MCP context history for the prompt
            var contextBuilder = new StringBuilder();
            
            // Add regular chat context using base implementation
            var baseContext = base.GetContextualPrompt(input);
            contextBuilder.AppendLine(baseContext);
            
            // Add MCP-specific context in the format Claude expects
            var recentMCPInteractions = GetRecentMCPInteractions();
            
            foreach (var mcpMessage in recentMCPInteractions)
            {
                switch (mcpMessage.Type)
                {
                    case "tool_use":
                        contextBuilder.AppendLine($"Tool Use: {mcpMessage.ToolName}");
                        contextBuilder.AppendLine($"Input: {JsonSerializer.Serialize(mcpMessage.Input)}");
                        break;
                    case "tool_result":
                        contextBuilder.AppendLine($"Tool Result: {mcpMessage.ToolName}");
                        if (mcpMessage.Success)
                        {
                            contextBuilder.AppendLine($"Result: {JsonSerializer.Serialize(mcpMessage.Result)}");
                        }
                        else
                        {
                            contextBuilder.AppendLine($"Error: {mcpMessage.Error}");
                        }
                        break;
                    case "retrieval_request":
                        contextBuilder.AppendLine("Retrieval Request:");
                        contextBuilder.AppendLine($"Query: {mcpMessage.Input["query"]}");
                        if (mcpMessage.Input.ContainsKey("source") && mcpMessage.Input["source"] != null)
                        {
                            contextBuilder.AppendLine($"Source: {mcpMessage.Input["source"]}");
                        }
                        break;
                    case "retrieval_result":
                        contextBuilder.AppendLine("Retrieval Result:");
                        contextBuilder.AppendLine($"Source: {mcpMessage.Input["source"]}");
                        contextBuilder.AppendLine($"Result: {JsonSerializer.Serialize(mcpMessage.Result)}");
                        break;
                    case "user_interaction":
                        contextBuilder.AppendLine("User Interaction:");
                        contextBuilder.AppendLine($"Prompt: {mcpMessage.Input["prompt"]}");
                        if (mcpMessage.Result != null)
                        {
                            contextBuilder.AppendLine($"Response: {mcpMessage.Result}");
                        }
                        break;
                }
                
                contextBuilder.AppendLine();
            }
            
            return contextBuilder.ToString();
        }
        
        /// <summary>
        /// Gets recent MCP interactions for context
        /// </summary>
        public List<MCPContextMessage> GetRecentMCPInteractions(int count = 10)
        {
            return _mcpContextHistory.TakeLast(count).ToList();
        }
        
        /// <summary>
        /// Enforces the maximum context size limit
        /// </summary>
        private void EnforceSizeLimit()
        {
            if (_mcpContextHistory.Count > _maxContextSize)
            {
                _mcpContextHistory = _mcpContextHistory.Skip(_mcpContextHistory.Count - _maxContextSize).ToList();
            }
        }
        
        /// <summary>
        /// Clears the context including MCP history
        /// </summary>
        public new void ClearContext()
        {
            base.ClearContext();
            _mcpContextHistory.Clear();
        }
        
        /// <summary>
        /// Gets debug information including MCP context
        /// </summary>
        public new string GetDebugInfo()
        {
            StringBuilder debug = new StringBuilder(base.GetDebugInfo());
            debug.AppendLine($"MCP Context Messages: {_mcpContextHistory.Count}");
            debug.AppendLine($"Recent Tool Uses: {_mcpContextHistory.Count(m => m.Type == "tool_use")}");
            debug.AppendLine($"Recent Tool Results: {_mcpContextHistory.Count(m => m.Type == "tool_result")}");
            return debug.ToString();
        }
    }
}