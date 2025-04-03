using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIAgent.Models.FunctionCalling;

namespace AIAgent.Services.Interfaces
{
    /// <summary>
    /// Interface for managing MCP tools
    /// </summary>
    public interface IToolRegistry : IFunctionRegistry
    {
        /// <summary>
        /// Registers a tool with the registry
        /// </summary>
        /// <param name="toolDefinition">Tool definition</param>
        /// <param name="handler">Handler function that executes the tool</param>
        void RegisterTool(ToolDefinition toolDefinition, Func<Dictionary<string, object>, Task<object>> handler);
        
        /// <summary>
        /// Gets all registered tools
        /// </summary>
        /// <returns>List of tool definitions</returns>
        List<ToolDefinition> GetTools();
        
        /// <summary>
        /// Gets tools by type
        /// </summary>
        /// <param name="toolType">Tool type to filter by</param>
        /// <returns>List of tool definitions matching the type</returns>
        List<ToolDefinition> GetToolsByType(string toolType);
        
        /// <summary>
        /// Gets tools by tag
        /// </summary>
        /// <param name="tag">Tag to filter by</param>
        /// <returns>List of tool definitions with the specified tag</returns>
        List<ToolDefinition> GetToolsByTag(string tag);
        
        /// <summary>
        /// Gets a tool definition by name
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>Tool definition or null if not found</returns>
        ToolDefinition GetToolDefinition(string name);
        
        /// <summary>
        /// Gets the handler function for a tool
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>Handler function or null if not found</returns>
        Func<Dictionary<string, object>, Task<object>> GetToolHandler(string name);
        
        /// <summary>
        /// Checks if a tool exists
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>True if the tool exists, false otherwise</returns>
        bool ToolExists(string name);
    }
}
