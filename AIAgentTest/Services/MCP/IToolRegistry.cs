using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Interface for registering and managing tools
    /// </summary>
    public interface IToolRegistry
    {
        /// <summary>
        /// Registers a tool with the registry
        /// </summary>
        /// <param name="toolDefinition">Definition of the tool</param>
        /// <param name="handler">Function that implements the tool</param>
        void RegisterTool(ToolDefinition toolDefinition, Func<Dictionary<string, object>, Task<object>> handler);
        
        /// <summary>
        /// Registers a function as a tool
        /// </summary>
        /// <param name="name">Name of the function</param>
        /// <param name="description">Description of the function</param>
        /// <param name="parameters">Parameters for the function</param>
        /// <param name="handler">Function that implements the tool</param>
        /// <param name="category">Category for the function</param>
        void RegisterFunction(
            string name, 
            string description, 
            Dictionary<string, ParameterDefinition> parameters, 
            Func<Dictionary<string, object>, Task<object>> handler,
            string category = "General");
        
        /// <summary>
        /// Gets all available tools
        /// </summary>
        /// <returns>List of tool definitions</returns>
        List<ToolDefinition> GetTools();
        
        /// <summary>
        /// Gets tools of a specific type
        /// </summary>
        /// <param name="toolType">Type of tool to filter by</param>
        /// <returns>List of tool definitions</returns>
        List<ToolDefinition> GetToolsByType(string toolType);
        
        /// <summary>
        /// Gets tools with a specific tag
        /// </summary>
        /// <param name="tag">Tag to filter by</param>
        /// <returns>List of tool definitions</returns>
        List<ToolDefinition> GetToolsByTag(string tag);
        
        /// <summary>
        /// Gets the definition of a specific tool
        /// </summary>
        /// <param name="name">Name of the tool</param>
        /// <returns>Tool definition or null if not found</returns>
        ToolDefinition GetToolDefinition(string name);
        
        /// <summary>
        /// Gets the handler for a specific tool
        /// </summary>
        /// <param name="name">Name of the tool</param>
        /// <returns>Tool handler function or null if not found</returns>
        Func<Dictionary<string, object>, Task<object>> GetToolHandler(string name);
        
        /// <summary>
        /// Checks if a tool exists in the registry
        /// </summary>
        /// <param name="name">Name of the tool</param>
        /// <returns>True if the tool exists</returns>
        bool ToolExists(string name);
        
        /// <summary>
        /// Gets all function definitions (for backwards compatibility)
        /// </summary>
        /// <returns>List of function definitions</returns>
        List<FunctionDefinition> GetFunctionDefinitions();
        
        /// <summary>
        /// Enables a tool for use
        /// </summary>
        /// <param name="toolName">Name of the tool to enable</param>
        void EnableTool(string toolName);
        
        /// <summary>
        /// Disables a tool
        /// </summary>
        /// <param name="toolName">Name of the tool to disable</param>
        void DisableTool(string toolName);
        
        /// <summary>
        /// Checks if a tool is enabled
        /// </summary>
        /// <param name="toolName">Name of the tool</param>
        /// <returns>True if the tool is enabled</returns>
        bool IsToolEnabled(string toolName);
        
        /// <summary>
        /// Gets all tool definitions including disabled ones
        /// </summary>
        /// <returns>List of all tool definitions</returns>
        List<ToolDefinition> GetAllToolDefinitions();
        
        /// <summary>
        /// Event that is triggered when tool enable/disable state changes
        /// </summary>
        event EventHandler ToolsChanged;
    }
}