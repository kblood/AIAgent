using System;
using System.Collections.Generic;

namespace AIAgent.Models.FunctionCalling
{
    /// <summary>
    /// Enhanced version of FunctionDefinition for MCP tools
    /// </summary>
    public class ToolDefinition : FunctionDefinition
    {
        /// <summary>
        /// Type of tool (function, retrieval, user_interaction)
        /// </summary>
        public string ToolType { get; set; } = "function";
        
        /// <summary>
        /// Input schema for the tool in JSON Schema format
        /// </summary>
        public Dictionary<string, object> Input { get; set; }
        
        /// <summary>
        /// Output schema for the tool in JSON Schema format
        /// </summary>
        public Dictionary<string, object> Output { get; set; }
        
        /// <summary>
        /// Example usages for the tool
        /// </summary>
        public List<string> Examples { get; set; } = new List<string>();
        
        /// <summary>
        /// Categorization tags
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();
        
        /// <summary>
        /// Additional tool metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}
