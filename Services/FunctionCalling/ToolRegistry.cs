using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIAgent.Models.FunctionCalling;
using AIAgent.Services.Interfaces;

namespace AIAgent.Services.FunctionCalling
{
    /// <summary>
    /// Implementation of IToolRegistry for managing MCP tools
    /// </summary>
    public class ToolRegistry : IToolRegistry
    {
        private readonly Dictionary<string, (ToolDefinition Definition, Func<Dictionary<string, object>, Task<object>> Handler)> _tools = new Dictionary<string, (ToolDefinition, Func<Dictionary<string, object>, Task<object>>)>();
        
        /// <summary>
        /// Registers a tool with the registry
        /// </summary>
        public void RegisterTool(
            ToolDefinition toolDefinition, 
            Func<Dictionary<string, object>, Task<object>> handler)
        {
            _tools[toolDefinition.Name] = (toolDefinition, handler);
        }
        
        /// <summary>
        /// Simplified registration method that builds the ToolDefinition
        /// </summary>
        public void RegisterTool(
            string name,
            string description,
            Dictionary<string, object> inputSchema,
            Dictionary<string, object> outputSchema,
            Func<Dictionary<string, object>, Task<object>> handler,
            string toolType = "function",
            List<string> tags = null)
        {
            var toolDefinition = new ToolDefinition
            {
                Name = name,
                Description = description,
                Input = inputSchema,
                Output = outputSchema,
                ToolType = toolType,
                Tags = tags ?? new List<string>()
            };
            
            RegisterTool(toolDefinition, handler);
        }
        
        /// <summary>
        /// Gets all registered tools
        /// </summary>
        public List<ToolDefinition> GetTools()
        {
            return _tools.Values.Select(t => t.Definition).ToList();
        }
        
        /// <summary>
        /// Gets tools by type
        /// </summary>
        public List<ToolDefinition> GetToolsByType(string toolType)
        {
            return _tools.Values
                .Where(t => t.Definition.ToolType == toolType)
                .Select(t => t.Definition)
                .ToList();
        }
        
        /// <summary>
        /// Gets tools by tag
        /// </summary>
        public List<ToolDefinition> GetToolsByTag(string tag)
        {
            return _tools.Values
                .Where(t => t.Definition.Tags.Contains(tag))
                .Select(t => t.Definition)
                .ToList();
        }
        
        /// <summary>
        /// Gets a tool definition by name
        /// </summary>
        public ToolDefinition GetToolDefinition(string name)
        {
            return _tools.TryGetValue(name, out var tool) ? tool.Definition : null;
        }
        
        /// <summary>
        /// Gets the handler function for a tool
        /// </summary>
        public Func<Dictionary<string, object>, Task<object>> GetToolHandler(string name)
        {
            return _tools.TryGetValue(name, out var tool) ? tool.Handler : null;
        }
        
        /// <summary>
        /// Checks if a tool exists
        /// </summary>
        public bool ToolExists(string name)
        {
            return _tools.ContainsKey(name);
        }
        
        #region IFunctionRegistry Compatibility Methods
        
        /// <summary>
        /// Registers a function (for I