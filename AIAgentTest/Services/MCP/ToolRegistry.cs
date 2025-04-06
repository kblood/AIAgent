using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Implementation of IToolRegistry
    /// </summary>
    public class ToolRegistry : IToolRegistry
    {
        private readonly Dictionary<string, ToolDefinition> _tools = new Dictionary<string, ToolDefinition>();
        private readonly Dictionary<string, Func<object, Task<object>>> _handlers = new Dictionary<string, Func<object, Task<object>>>();
        private readonly HashSet<string> _enabledTools = new HashSet<string>();
        
        /// <summary>
        /// Event that fires when tools change
        /// </summary>
        public event EventHandler ToolsChanged;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ToolRegistry()
        {
            // Load enabled tools from settings
            LoadEnabledTools();
        }
        
        /// <summary>
        /// Register a tool
        /// </summary>
        /// <param name="definition">Tool definition</param>
        /// <param name="handler">Tool handler function</param>
        public void RegisterTool(ToolDefinition definition, Func<object, Task<object>> handler)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));
            
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            
            if (string.IsNullOrWhiteSpace(definition.Name))
                throw new ArgumentException("Tool name cannot be empty");
            
            // Add the tool
            _tools[definition.Name] = definition;
            _handlers[definition.Name] = handler;
            
            // Enable by default if not explicitly disabled
            if (!_enabledTools.Contains(definition.Name) && !IsToolExplicitlyDisabled(definition.Name))
            {
                _enabledTools.Add(definition.Name);
            }
            
            // Notify about the change
            OnToolsChanged();
            
            // Save enabled tools to settings
            SaveEnabledTools();
        }
        
        /// <summary>
        /// Get all tools
        /// </summary>
        /// <returns>List of tools</returns>
        public List<ToolDefinition> GetTools()
        {
            return _tools
                .Where(t => _enabledTools.Contains(t.Key))
                .Select(t => t.Value)
                .ToList();
        }
        
        /// <summary>
        /// Get a specific tool handler
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>Tool handler function</returns>
        public Func<object, Task<object>> GetToolHandler(string name)
        {
            if (!_handlers.TryGetValue(name, out var handler))
                return null;
            
            if (!_enabledTools.Contains(name))
                return null;
            
            return handler;
        }
        
        /// <summary>
        /// Get a specific tool definition
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>Tool definition</returns>
        public ToolDefinition GetToolDefinition(string name)
        {
            if (_tools.TryGetValue(name, out var definition))
                return definition;
            
            return null;
        }
        
        /// <summary>
        /// Check if a tool exists
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>True if tool exists</returns>
        public bool ToolExists(string name)
        {
            return _tools.ContainsKey(name);
        }
        
        /// <summary>
        /// Enable a tool
        /// </summary>
        /// <param name="name">Tool name</param>
        public void EnableTool(string name)
        {
            if (!_tools.ContainsKey(name))
                return;
            
            _enabledTools.Add(name);
            
            // Notify about the change
            OnToolsChanged();
            
            // Save enabled tools to settings
            SaveEnabledTools();
        }
        
        /// <summary>
        /// Disable a tool
        /// </summary>
        /// <param name="name">Tool name</param>
        public void DisableTool(string name)
        {
            _enabledTools.Remove(name);
            
            // Notify about the change
            OnToolsChanged();
            
            // Save enabled tools to settings
            SaveEnabledTools();
        }
        
        /// <summary>
        /// Check if a tool is enabled
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>True if enabled</returns>
        public bool IsToolEnabled(string name)
        {
            return _enabledTools.Contains(name);
        }
        
        /// <summary>
        /// Get all tools including their definitions
        /// </summary>
        /// <returns>List of all tools</returns>
        public List<ToolDefinition> GetAllTools()
        {
            return _tools.Values.ToList();
        }
        
        /// <summary>
        /// Get all tool definitions including disabled tools
        /// </summary>
        /// <returns>List of tool definitions</returns>
        public List<ToolDefinition> GetAllToolDefinitions()
        {
            return _tools.Values.ToList();
        }
        
        /// <summary>
        /// Check if a tool is explicitly disabled in settings
        /// </summary>
        /// <param name="name">Tool name</param>
        /// <returns>True if disabled</returns>
        private bool IsToolExplicitlyDisabled(string name)
        {
            var disabledTools = Properties.Settings.Default.EnabledTools?
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.StartsWith("!"))
                .Select(t => t.Substring(1))
                .ToList() ?? new List<string>();
            
            return disabledTools.Contains(name);
        }
        
        /// <summary>
        /// Load enabled tools from settings
        /// </summary>
        private void LoadEnabledTools()
        {
            var enabledToolsString = Properties.Settings.Default.EnabledTools;
            
            if (string.IsNullOrEmpty(enabledToolsString))
                return;
            
            var toolsList = enabledToolsString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var tool in toolsList)
            {
                if (tool.StartsWith("!"))
                    continue; // Explicitly disabled tool, handled in IsToolExplicitlyDisabled
                
                _enabledTools.Add(tool);
            }
        }
        
        /// <summary>
        /// Save enabled tools to settings
        /// </summary>
        private void SaveEnabledTools()
        {
            var allTools = _tools.Keys.ToList();
            var enabledTools = new List<string>();
            var disabledTools = new List<string>();
            
            foreach (var tool in allTools)
            {
                if (_enabledTools.Contains(tool))
                    enabledTools.Add(tool);
                else
                    disabledTools.Add("!" + tool);
            }
            
            Properties.Settings.Default.EnabledTools = string.Join(";", enabledTools.Concat(disabledTools));
            Properties.Settings.Default.Save();
        }
        
        /// <summary>
        /// Trigger the ToolsChanged event
        /// </summary>
        private void OnToolsChanged()
        {
            ToolsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
