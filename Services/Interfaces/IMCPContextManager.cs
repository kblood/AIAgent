using System;
using System.Collections.Generic;
using AIAgent.Models.FunctionCalling;

namespace AIAgent.Services.Interfaces
{
    /// <summary>
    /// Interface for managing MCP context
    /// </summary>
    public interface IMCPContextManager : IContextManager
    {
        /// <summary>
        /// Adds a tool use to the context
        /// </summary>
        /// <param name="toolName">Name of the tool used</param>
        /// <param name="input">Input parameters for the tool</param>
        void AddToolUse(string toolName, Dictionary<string, object> input);
        
        /// <summary>
        /// Adds a tool result to the context
        /// </summary>
        /// <param name="toolName">Name of the tool</param>
        /// <param name="result">Result returned by the tool</param>
        /// <param name="success">Whether the tool execution succeeded</param>
        /// <param name="error">Error message if the tool execution failed</param>
        void AddToolResult(string toolName, object result, bool success, string error = null);
        
        /// <summary>
        /// Adds a retrieval request to the context
        /// </summary>
        /// <param name="query">Retrieval query</param>
        /// <param name="source">Source to retrieve from (optional)</param>
        void AddRetrievalRequest(string query, string source = null);
        
        /// <summary>
        /// Adds a retrieval result to the context
        /// </summary>
        /// <param name="source">Source of the retrieved information</param>
        /// <param name="result">Retrieved information</param>
        void AddRetrievalResult(string source, object result);
        
        /// <summary>
        /// Adds a user interaction to the context
        /// </summary>
        /// <param name="prompt">Prompt shown to the user</param>
        /// <param name="response">User's response (null if not yet provided)</param>
        void AddUserInteraction(string prompt, string response = null);
        
        /// <summary>
        /// Gets a contextual prompt with MCP context
        /// </summary>
        /// <param name="input">User input</param>
        /// <returns>Prompt with MCP context</returns>
        string GetMCPContextualPrompt(string input);
        
        /// <summary>
        /// Gets recent MCP interactions
        /// </summary>
        /// <param name="count">Maximum number of interactions to return</param>
        /// <returns>List of recent MCP context messages</returns>
        List<MCPContextMessage> GetRecentMCPInteractions(int count = 10);
    }
}
