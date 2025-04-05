using System.Collections.Generic;

namespace AIAgentTest.Common
{
    /// <summary>
    /// Unified parameter definition for functions and tools
    /// Used for both standard function calling and MCP tools
    /// </summary>
    public class ParameterDefinition
    {
        /// <summary>
        /// Type of the parameter
        /// </summary>
        public string Type { get; set; }
        
        /// <summary>
        /// Description of the parameter
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Whether the parameter is required
        /// </summary>
        public bool Required { get; set; }
        
        /// <summary>
        /// Default value for the parameter
        /// </summary>
        public object DefaultValue { get; set; }
        
        /// <summary>
        /// Constraints for the parameter
        /// </summary>
        public Dictionary<string, object> Constraints { get; set; } = new Dictionary<string, object>();
    }
}