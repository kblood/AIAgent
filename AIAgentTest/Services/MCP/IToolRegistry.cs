using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Interface for tool registry
    /// </summary>
    public interface IToolRegistry
    {
        /// <summary>
        /// Event that fires when tools change
        /// </summary>
        event EventHandler ToolsChanged;
        
        /// <summary>
        /// Register a tool
        /// </summary>
        /// <param name="definition">Tool definition</param>
        /// <param name="handler">Tool handler function</param>
        void RegisterTool(ToolDefinition definition, Func<object, Task<object>> handler);
        
        /// <summary>
        /// Get all tools
        /// </summary>
        /// <returns>List of tools</returns>
        List<ToolDefinition> GetTools();
        
        /// <summary>
        /// Get a specific tool handler
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>Tool handler function</returns>
        Func<object, Task<object>> GetToolHandler(string name);
        
        /// <summary>
        /// Get a specific tool definition
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>Tool definition</returns>
        ToolDefinition GetToolDefinition(string name);
        
        /// <summary>
        /// Check if a tool exists
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>True if tool exists</returns>
        bool ToolExists(string name);
        
        /// <summary>
        /// Enable a tool
        /// </summary>
        /// <param name="name">Tool name</param>
        void EnableTool(string name);
        
        /// <summary>
        /// Disable a tool
        /// </summary>
        /// <param name="name">Tool name</param>
        void DisableTool(string name);
        
        /// <summary>
        /// Check if a tool is enabled
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>True if enabled</returns>
        bool IsToolEnabled(string name);
        
        /// <summary>
        /// Get all tool definitions including disabled tools
        /// </summary>
        /// <returns>List of tool definitions</returns>
        List<ToolDefinition> GetAllToolDefinitions();
    }
}
