using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIAgentTest.API_Clients;
using AIAgentTest.API_Clients.MCP;
using AIAgentTest.Services.Interfaces;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Service for interacting with MCP-capable LLM clients
    /// </summary>
    public class MCPLLMClientService : LLMClientService, IMCPLLMClientService
    {
        private readonly MCPClientFactory _mcpClientFactory;
        private readonly IToolRegistry _toolRegistry;
        
        /// <summary>
        /// Creates a new MCPLLMClientService
        /// </summary>
        /// <param name="llmClient">Base LLM client</param>
        /// <param name="mcpClientFactory">Factory for creating MCP clients</param>
        /// <param name="toolRegistry">Registry of available tools</param>
        public MCPLLMClientService(
            ILLMClient llmClient,
            MCPClientFactory mcpClientFactory,
            IToolRegistry toolRegistry) 
            : base(llmClient)
        {
            _mcpClientFactory = mcpClientFactory ?? throw new ArgumentNullException(nameof(mcpClientFactory));
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        }
        
        /// <summary>
        /// Generates a response using MCP with tools/functions
        /// </summary>
        public async Task<MCPResponse> GenerateWithMCPAsync(string prompt, string model, List<ToolDefinition> tools)
        {
            var mcpClient = GetMCPClient(model);
            return await mcpClient.GenerateWithMCPAsync(prompt, model, tools);
        }
        
        /// <summary>
        /// Continues a conversation after a tool has been executed
        /// </summary>
        public async Task<MCPResponse> ContinueWithToolResultAsync(string prompt, string toolName, object toolResult, string model)
        {
            var mcpClient = GetMCPClient(model);
            return await mcpClient.ContinueWithToolResultAsync(prompt, toolName, toolResult, model);
        }
        
        /// <summary>
        /// Generates a response with context from previous MCP interactions
        /// </summary>
        public async Task<string> GenerateWithContextAsync(string prompt, List<MCPContextMessage> contextMessages, string model)
        {
            var mcpClient = GetMCPClient(model);
            return await mcpClient.GenerateWithContextAsync(prompt, contextMessages, model);
        }
        
        /// <summary>
        /// Checks if a model supports MCP
        /// </summary>
        public bool ModelSupportsMCP(string model)
        {
            try
            {
                var mcpClient = GetMCPClient(model);
                return mcpClient.SupportsMCP;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Gets an MCP client for the specified model
        /// </summary>
        /// <param name="model">Model name</param>
        /// <returns>MCP-capable LLM client</returns>
        private IMCPLLMClient GetMCPClient(string model)
        {
            return _mcpClientFactory.GetClientForModel(model);
        }
        
        /// <summary>
        /// Executes a tool and returns the result
        /// </summary>
        /// <param name="toolName">Name of the tool to execute</param>
        /// <param name="parameters">Parameters for the tool</param>
        /// <returns>Tool execution result</returns>
        public async Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters)
        {
            var handler = _toolRegistry.GetToolHandler(toolName);
            if (handler == null)
            {
                throw new ArgumentException($"Tool '{toolName}' not found");
            }
            
            return await handler(parameters);
        }
    }
}