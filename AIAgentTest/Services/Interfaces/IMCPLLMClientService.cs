using System.Threading.Tasks;
using System.Collections.Generic;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.Services.Interfaces
{
    /// <summary>
    /// Interface for MCP-aware LLM client service
    /// </summary>
    public interface IMCPLLMClientService : ILLMClientService
    {
        /// <summary>
        /// Generate with MCP
        /// </summary>
        /// <param name="prompt">Prompt</param>
        /// <param name="model">Model name</param>
        /// <param name="tools">Tools</param>
        /// <returns>MCP response</returns>
        Task<MCPResponse> GenerateWithMCPAsync(string prompt, string model, List<ToolDefinition> tools);
        
        /// <summary>
        /// Continue with tool result
        /// </summary>
        /// <param name="originalInput">Original input</param>
        /// <param name="toolName">Tool name</param>
        /// <param name="toolResult">Tool result</param>
        /// <param name="model">Model name</param>
        /// <returns>MCP response</returns>
        Task<MCPResponse> ContinueWithToolResultAsync(string originalInput, string toolName, object toolResult, string model);
        
        /// <summary>
        /// Check if a model supports MCP
        /// </summary>
        /// <param name="model">Model name</param>
        /// <returns>True if MCP is supported</returns>
        bool ModelSupportsMCP(string model);
    }
}
