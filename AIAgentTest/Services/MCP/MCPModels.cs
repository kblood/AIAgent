using System;
using System.Collections.Generic;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Enhanced version of the FunctionDefinition that supports MCP tools
    /// </summary>
    public class ToolDefinition : FunctionDefinition
    {
        /// <summary>
        /// The type of tool (function, retrieval, user_interaction, etc.)
        /// </summary>
        public string ToolType { get; set; } = "function";
        
        /// <summary>
        /// The input schema in JSON Schema format
        /// </summary>
        public Dictionary<string, object> Input { get; set; }
        
        /// <summary>
        /// The output schema in JSON Schema format
        /// </summary>
        public Dictionary<string, object> Output { get; set; }
        
        /// <summary>
        /// Example usage of the tool
        /// </summary>
        public List<string> Examples { get; set; } = new List<string>();
        
        /// <summary>
        /// Tags for categorization
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();
        
        /// <summary>
        /// Additional metadata about the tool
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
    
    /// <summary>
    /// Represents a message in the MCP context
    /// </summary>
    public class MCPContextMessage
    {
        /// <summary>
        /// Type of context message (tool_use, tool_result, retrieval, etc.)
        /// </summary>
        public string Type { get; set; }
        
        /// <summary>
        /// Name of the tool if applicable
        /// </summary>
        public string ToolName { get; set; }
        
        /// <summary>
        /// Input parameters for the tool
        /// </summary>
        public Dictionary<string, object> Input { get; set; }
        
        /// <summary>
        /// Result from the tool if applicable
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
        /// Timestamp of the context message
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
    
    /// <summary>
    /// Represents a response from an MCP-enabled LLM
    /// </summary>
    public class MCPResponse
    {
        /// <summary>
        /// Type of response (text, tool_use, retrieval_request, etc.)
        /// </summary>
        public string Type { get; set; }
        
        /// <summary>
        /// Text content if it's a text response
        /// </summary>
        public string Text { get; set; }
        
        /// <summary>
        /// Name of the tool if it's a tool_use response
        /// </summary>
        public string Tool { get; set; }
        
        /// <summary>
        /// Input parameters for the tool if it's a tool_use response
        /// </summary>
        public Dictionary<string, object> Input { get; set; }
        
        /// <summary>
        /// Additional metadata about the response
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}