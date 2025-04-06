using System;
using System.Collections.Generic;
using System.Text.Json;
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
        [JsonPropertyName("name")] 
        public string Name { get; set; }

        /// <summary>
        /// Description of the tool
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        // --- NEW Property to capture the raw schema ---
        /// <summary>
        /// Holds the raw JSON schema object received from the server's 'inputSchema' field.
        /// Processed after deserialization to populate compatible fields.
        /// </summary>
        [JsonPropertyName("inputSchema")] // Map JSON 'inputSchema' to this
        public JsonElement RawInputSchema { get; set; } // Use JsonElement to hold arbitrary JSON
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

        /// <summary>
        /// Processes the RawInputSchema (received from JSON) to populate
        /// the compatibility fields (Input dictionary, Schema string).
        /// Should be called after the ToolDefinition object is deserialized.
        /// </summary>
        public void ProcessRawInputSchema()
        {
            // Ensure Input dictionary exists
            Input ??= new Dictionary<string, object>();
            Input.Clear(); // Clear any potential defaults

            // Check if RawInputSchema has been populated and is an object
            if (RawInputSchema.ValueKind == JsonValueKind.Object)
            {
                // 1. Populate the Schema string (for compatibility if needed)
                try
                {
                    // Pretty print for readability if stored/displayed
                    Schema = JsonSerializer.Serialize(RawInputSchema, new JsonSerializerOptions { WriteIndented = true });
                }
                catch
                {
                    Schema = RawInputSchema.GetRawText(); // Fallback to raw text
                }

                // 2. Populate the Input dictionary (Simplified Representation)
                if (RawInputSchema.TryGetProperty("properties", out var propertiesElement) && propertiesElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in propertiesElement.EnumerateObject())
                    {
                        string propertyName = property.Name;
                        JsonElement propertySchema = property.Value.Clone(); // Clone element

                        // Store the schema definition (JsonElement) for the property in the Input dict
                        // Code using the Input dict will need to handle this JsonElement.
                        // Alternative: Store just the type string if simpler compatibility is needed:
                        // if (propertySchema.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String)
                        // {
                        //     Input[propertyName] = typeElement.GetString();
                        // } else { Input[propertyName] = propertySchema; }

                        Input[propertyName] = propertySchema;
                    }
                }
                // else: Schema has no 'properties' object. Input dictionary remains empty.

                // Optionally: You could also try and populate 'Parameters' here if you
                // can map the JsonSchema structure to your MCPParameterDefinition structure,
                // but it might be complex.
            }
            else
            {
                // RawInputSchema was not populated or was not a JSON object.
                Schema = null; // Clear schema string
                // Input dictionary remains empty.
            }
        }
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
