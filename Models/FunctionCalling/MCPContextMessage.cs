using System;
using System.Collections.Generic;

namespace AIAgent.Models.FunctionCalling
{
    /// <summary>
    /// Represents a message in the MCP context history
    /// </summary>
    public class MCPContextMessage
    {
        /// <summary>
        /// Type of context message ("tool_use", "tool_result", "retrieval", etc.)
        /// </summary>
        public string Type { get; set; }
        
        /// <summary>
        /// Name of the tool used (if applicable)
        /// </summary>
        public string ToolName { get; set; }
        
        /// <summary>
        /// Input parameters for the tool
        /// </summary>
        public Dictionary<string, object> Input { get; set; }
        
        /// <summary>
        /// Result returned by the tool
        /// </summary>
        public object Result { get; set; }
        
        /// <summary>
        /// Whether the tool execution succeeded
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Error message if the tool execution failed
        /// </summary>
        public string Error { get; set; }
        
        /// <summary>
        /// Timestamp of the interaction
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
