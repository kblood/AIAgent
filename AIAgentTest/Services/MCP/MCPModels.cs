using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using AIAgentTest.Common;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Definition of a tool for MCP
    /// </summary>
    public class ToolDefinition
    {
        /// <summary>
        /// Name of the tool
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Description of the tool
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Tool schema in JSON format
        /// </summary>
        public string Schema { get; set; }
        
        /// <summary>
        /// Tool tags for categorization
        /// </summary>
        public string[] Tags { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// Input schema
        /// </summary>
        public Dictionary<string, object> Input { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// Output schema
        /// </summary>
        public Dictionary<string, object> Output { get; set; } = new Dictionary<string, object>();
        
        /// <summary>
        /// Parameters for the tool
        /// </summary>
        public Dictionary<string, MCPParameterDefinition> Parameters { get; set; } = new Dictionary<string, MCPParameterDefinition>();
        
        /// <summary>
        /// Tool type
        /// </summary>
        public string ToolType { get; set; }
        
        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }
    }
    
    /// <summary>
    /// Context message for MCP
    /// </summary>
    public class MCPContextMessage
    {
        /// <summary>
        /// Role of the sender (user, assistant, system, tool)
        /// </summary>
        public string Role { get; set; }
        
        /// <summary>
        /// Content of the message
        /// </summary>
        public string Content { get; set; }
        
        /// <summary>
        /// Name of the tool if role is "tool"
        /// </summary>
        public string ToolName { get; set; }
        
        /// <summary>
        /// Tool input
        /// </summary>
        public object ToolInput { get; set; }
        
        /// <summary>
        /// Tool result
        /// </summary>
        public object ToolResult { get; set; }
        
        /// <summary>
        /// Type of message (tool_use, tool_result, etc.)
        /// </summary>
        public string Type { get; set; }
        
        /// <summary>
        /// Input parameters dictionary
        /// </summary>
        public Dictionary<string, object> Input { get; set; }
        
        /// <summary>
        /// Result of the operation
        /// </summary>
        public object Result { get; set; }
        
        /// <summary>
        /// Whether the operation succeeded
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        public string Error { get; set; }
        
        /// <summary>
        /// Additional metadata about the message
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }
    }
    
    /// <summary>
    /// Response from MCP
    /// </summary>
    public class MCPResponse
    {
        /// <summary>
        /// Type of response (text, tool_use)
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        /// <summary>
        /// Text response
        /// </summary>
        [JsonPropertyName("text")]
        public string Text { get; set; }
        
        /// <summary>
        /// Tool name for tool_use response
        /// </summary>
        [JsonPropertyName("tool")]
        public string Tool { get; set; }
        
        /// <summary>
        /// Tool input parameters
        /// </summary>
        [JsonPropertyName("input")]
        public object Input { get; set; }
        
        /// <summary>
        /// Additional metadata
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }
    }
}
