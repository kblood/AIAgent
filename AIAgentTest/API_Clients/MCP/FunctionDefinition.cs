using System.Collections.Generic;

namespace AIAgentTest.API_Clients.MCP
{
    /// <summary>
    /// Parameter definition for a function
    /// </summary>
    public class ParameterDefinition
    {
        /// <summary>
        /// Parameter type
        /// </summary>
        public string Type { get; set; }
        
        /// <summary>
        /// Parameter description
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Whether the parameter is required
        /// </summary>
        public bool Required { get; set; }
    }
    
    /// <summary>
    /// Definition of a function for function calling
    /// </summary>
    public class FunctionDefinition
    {
        /// <summary>
        /// Function name
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Function description
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Function parameters
        /// </summary>
        public Dictionary<string, ParameterDefinition> Parameters { get; set; }
    }
}
