using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Implementation of IToolRegistry for managing tools and functions
    /// </summary>
    public class ToolRegistry : IToolRegistry
    {
        private readonly Dictionary<string, (ToolDefinition Definition, Func<Dictionary<string, object>, Task<object>> Handler)> _tools = new();
        
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
        /// Gets all available tools
        /// </summary>
        public List<ToolDefinition> GetTools()
        {
            return _tools.Values.Select(t => t.Definition).ToList();
        }
        
        /// <summary>
        /// Gets tools of a specific type
        /// </summary>
        public List<ToolDefinition> GetToolsByType(string toolType)
        {
            return _tools.Values
                .Where(t => t.Definition.ToolType == toolType)
                .Select(t => t.Definition)
                .ToList();
        }
        
        /// <summary>
        /// Gets tools with a specific tag
        /// </summary>
        public List<ToolDefinition> GetToolsByTag(string tag)
        {
            return _tools.Values
                .Where(t => t.Definition.Tags.Contains(tag))
                .Select(t => t.Definition)
                .ToList();
        }
        
        /// <summary>
        /// Gets the definition of a specific tool
        /// </summary>
        public ToolDefinition GetToolDefinition(string name)
        {
            return _tools.TryGetValue(name, out var tool) ? tool.Definition : null;
        }
        
        /// <summary>
        /// Gets the handler for a specific tool
        /// </summary>
        public Func<Dictionary<string, object>, Task<object>> GetToolHandler(string name)
        {
            return _tools.TryGetValue(name, out var tool) ? tool.Handler : null;
        }
        
        /// <summary>
        /// Checks if a tool exists in the registry
        /// </summary>
        public bool ToolExists(string name)
        {
            return _tools.ContainsKey(name);
        }
        
        /// <summary>
        /// Registers a function as a tool
        /// </summary>
        public void RegisterFunction(
            string name, 
            string description, 
            Dictionary<string, ParameterDefinition> parameters, 
            Func<Dictionary<string, object>, Task<object>> handler,
            string category = "General")
        {
            // Convert ParameterDefinition to MCP input schema
            var properties = new Dictionary<string, object>();
            var required = new List<string>();
            
            foreach (var param in parameters)
            {
                properties[param.Key] = new Dictionary<string, object>
                {
                    ["type"] = param.Value.Type.ToLower(),
                    ["description"] = param.Value.Description
                };
                
                if (param.Value.Required)
                {
                    required.Add(param.Key);
                }
            }
            
            var inputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required
            };
            
            // Create a generic output schema
            var outputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["description"] = "Function result"
            };
            
            // Register as a tool
            RegisterTool(
                name: name,
                description: description,
                inputSchema: inputSchema,
                outputSchema: outputSchema,
                handler: handler,
                toolType: "function",
                tags: new List<string> { category }
            );
        }
        
        /// <summary>
        /// Gets all function definitions (for backwards compatibility)
        /// </summary>
        public List<FunctionDefinition> GetFunctionDefinitions()
        {
            // Convert tools to function definitions for backward compatibility
            return GetToolsByType("function")
                .Select(t => new FunctionDefinition 
                { 
                    Name = t.Name, 
                    Description = t.Description,
                    Parameters = ConvertToolParametersToFunctionParameters(t.Input)
                })
                .ToList();
        }
        
        /// <summary>
        /// Converts tool parameters to function parameters
        /// </summary>
        private Dictionary<string, ParameterDefinition> ConvertToolParametersToFunctionParameters(Dictionary<string, object> input)
        {
            var result = new Dictionary<string, ParameterDefinition>();
            
            if (input.TryGetValue("properties", out var propertiesObj) && propertiesObj is Dictionary<string, object> properties)
            {
                var required = new List<string>();
                if (input.TryGetValue("required", out var requiredObj) && requiredObj is List<string> requiredList)
                {
                    required = requiredList;
                }
                
                foreach (var prop in properties)
                {
                    if (prop.Value is Dictionary<string, object> propDetails)
                    {
                        var paramDef = new ParameterDefinition
                        {
                            Type = propDetails.TryGetValue("type", out var typeObj) ? typeObj.ToString() : "string",
                            Description = propDetails.TryGetValue("description", out var descObj) ? descObj.ToString() : "",
                            Required = required.Contains(prop.Key)
                        };
                        
                        result[prop.Key] = paramDef;
                    }
                }
            }
            
            return result;
        }
    }
}