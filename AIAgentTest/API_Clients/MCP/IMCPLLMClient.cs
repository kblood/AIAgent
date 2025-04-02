using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.API_Clients.MCP
{
    /// <summary>
    /// Interface for LLM clients that support the Model Context Protocol (MCP)
    /// </summary>
    public interface IMCPLLMClient : ILLMClient
    {
        /// <summary>
        /// Indicates whether the client supports MCP
        /// </summary>
        bool SupportsMCP { get; }
        
        /// <summary>
        /// Generates a response using MCP with tools/functions
        /// </summary>
        /// <param name="prompt">The user's input</param>
        /// <param name="model">The model to use</param>
        /// <param name="tools">The available tools/functions</param>
        /// <returns>MCP response which may include tool usage</returns>
        Task<MCPResponse> GenerateWithMCPAsync(string prompt, string model, List<ToolDefinition> tools);
        
        /// <summary>
        /// Continues a conversation after a tool has been executed
        /// </summary>
        /// <param name="prompt">The original prompt</param>
        /// <param name="toolName">The name of the tool that was used</param>
        /// <param name="toolResult">The result from executing the tool</param>
        /// <param name="model">The model to use</param>
        /// <returns>MCP response which may include another tool usage</returns>
        Task<MCPResponse> ContinueWithToolResultAsync(string prompt, string toolName, object toolResult, string model);
        
        /// <summary>
        /// Generates a response with context from previous MCP interactions
        /// </summary>
        /// <param name="prompt">The user's input</param>
        /// <param name="contextMessages">Previous MCP context messages</param>
        /// <param name="model">The model to use</param>
        /// <returns>Generated text response</returns>
        Task<string> GenerateWithContextAsync(string prompt, List<MCPContextMessage> contextMessages, string model);
    }
}