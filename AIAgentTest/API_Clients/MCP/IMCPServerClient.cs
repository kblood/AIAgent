using System.Collections.Generic;
using System.Threading.Tasks;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.API_Clients.MCP
{
    /// <summary>
    /// Interface for MCP server clients
    /// </summary>
    public interface IMCPServerClient
    {
        /// <summary>
        /// Gets the available tools from the server
        /// </summary>
        /// <returns>List of tool definitions</returns>
        Task<List<ToolDefinition>> GetAvailableToolsAsync();
        
        /// <summary>
        /// Executes a tool on the server
        /// </summary>
        /// <param name="toolName">Name of the tool to execute</param>
        /// <param name="parameters">Parameters for the tool</param>
        /// <returns>Result of the tool execution</returns>
        Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters);
        
        /// <summary>
        /// Checks if the server is available
        /// </summary>
        /// <returns>True if the server is available, false otherwise</returns>
        Task<bool> IsAvailableAsync();
    }
}
