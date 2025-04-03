using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIAgent.Models.FunctionCalling;

namespace AIAgent.Services.Interfaces
{
    /// <summary>
    /// Interface for LLM clients that support the Model Context Protocol (MCP)
    /// </summary>
    public interface IMCPLLMClient : ILLMClient
    {
        /// <summary>
        /// Whether this client supports MCP capabilities
        /// </summary>
        bool SupportsMCP { get; }
        
        /// <summary>
        /// Generates a response using MCP with access to tools
        /// </summary>
        /// <param name="prompt">User prompt</param>
        /// <param name="model">Model to use</param>
        /// <param name="tools">Available tools</param>
        /// <returns>MCP response which may be text or a tool use</returns>
        Task<MCPResponse> GenerateWithMCPAsync(string prompt, string model, List<ToolDefinition> tools);
        
        /// <summary>
        /// Continues the conversation with a tool result
        /// </summary>
        /// <param name="prompt">Original prompt that triggered the tool use</param>
        /// <param name="toolName">Name of the tool that was used</param>
        /// <param name="toolResult">Result returned by the tool</param>
        /// <param name="model">Model to use</param>
        /// <returns>MCP response which may be text or another tool use</returns>
        Task<MCPResponse> ContinueWithToolResultAsync(string prompt, string toolName, object toolResult, string model);
        
        /// <summary>
        /// Generates a response with context from previous MCP interactions
        /// </summary>
        /// <param name="prompt">User prompt</param>
        /// <param name="contextMessages">MCP context messages from previous interactions</param>
        /// <param name="model">Model to use</param>
        /// <returns>Generated response text</returns>
        Task<string> GenerateWithContextAsync(string prompt, List<MCPContextMessage> contextMessages, string model);
    }
}
