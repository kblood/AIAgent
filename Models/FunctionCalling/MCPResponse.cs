using System;
using System.Collections.Generic;

namespace AIAgent.Models.FunctionCalling
{
    /// <summary>
    /// Represents a response from an MCP-enabled model
    /// </summary>
    public class MCPResponse
    {
        /// <summary>
        /// Type of response ("text", "tool_use", "retrieval_request", etc.)
        /// </summary>
        public string Type { get; set; }
        
        /// <summary>
        /// Text content of the response (for text responses)
        /// </summary>
        public string Text { get; set; }
        
        /// <summary>
        /// Name of the tool to use (for tool_use responses)
        /// </summary>
        public string Tool { get; set; }
        
        /// <summary>
        /// Input parameters for the tool (for tool_use responses)
        /// </summary>
        public Dictionary<string, object> Input { get; set; }
        
        /// <summary>
        /// Additional metadata about the response
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}
