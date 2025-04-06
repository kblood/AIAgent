using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.API_Clients.MCP
{
    /// <summary>
    /// Interface for MCP server clients
    /// </summary>
    public interface IMCPServerClient : IDisposable
    {
        /// <summary>
        /// Get available tools from the server
        /// </summary>
        /// <returns>List of tool definitions</returns>
        Task<List<ToolDefinition>> GetToolsAsync();
        
        /// <summary>
        /// Execute a tool on the server
        /// </summary>
        /// <param name="toolName">Tool name</param>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        Task<object> ExecuteToolAsync(string toolName, object input);
        
        /// <summary>
        /// Check if the server is available
        /// </summary>
        /// <returns>True if available</returns>
        Task<bool> IsAvailableAsync();
        
        /// <summary>
        /// Start the server process
        /// </summary>
        /// <returns>Whether the server was started successfully</returns>
        Task<bool> StartServerAsync();
        
        /// <summary>
        /// Stop the server process
        /// </summary>
        void StopServer();
    }
}
