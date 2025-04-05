using System.Collections.Generic;
using AIAgentTest.Common;

namespace AIAgentTest.API_Clients.MCP
{
    // Using common ParameterDefinition from AIAgentTest.Common
    
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
