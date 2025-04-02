using System.Collections.Generic;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.Services.Interfaces
{
    /// <summary>
    /// Extends the IContextManager interface with MCP capabilities
    /// </summary>
    public interface IMCPContextManager : IContextManager
    {
        /// <summary>
        /// Adds a tool use to the context
        /// </summary>
        /// <param name="toolName">Name of the tool</param>
        /// <param name="input">Input parameters for the tool</param>
        void AddToolUse(string toolName, Dictionary<string, object> input);
        
        /// <summary>
        /// Adds a tool result to the context
        /// </summary>
        /// <param name="toolName">Name of the tool</param>
        /// <param name="result">Result from executing the tool</param>
        /// <param name="success">Whether the execution succeeded</param>
        /// <param name="error">Error message if execution failed</param>
        void AddToolResult(string toolName, object result, bool success, string error = null);
        
        /// <summary>
        /// Adds a retrieval request to the context
        /// </summary>
        /// <param name="query">The retrieval query</param>
        /// <param name="source">Optional source of the retrieval</param>
        void AddRetrievalRequest(string query, string source = null);
        
        /// <summary>
        /// Adds a retrieval result to the context
        /// </summary>
        /// <param name="source">Source of the retrieved information</param>
        /// <param name="result">The retrieved information</param>
        void AddRetrievalResult(string source, object result);
        
        /// <summary>
        /// Adds a user interaction to the context
        /// </summary>
        /// <param name="prompt">The prompt shown to the user</param>
        /// <param name="response">The user's response</param>
        void AddUserInteraction(string prompt, string response = null);
        
        /// <summary>
        /// Gets a prompt with MCP context included
        /// </summary>
        /// <param name="input">The user's input</param>
        /// <returns>Prompt with MCP context</returns>
        string GetMCPContextualPrompt(string input);
        
        /// <summary>
        /// Gets recent MCP interactions for context
        /// </summary>
        /// <param name="count">Maximum number of interactions to return</param>
        /// <returns>List of recent MCP context messages</returns>
        List<MCPContextMessage> GetRecentMCPInteractions(int count = 10);
    }
}