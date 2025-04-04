using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIAgentTest.API_Clients;
using AIAgentTest.API_Clients.MCP;
using AIAgentTest.Services.Interfaces;
using System.Text.Json;
using System.Linq;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// MCP-aware LLM client service
    /// </summary>
    public class MCPLLMClientService : IMCPLLMClientService
    {
        private readonly ILLMClient _llmClient;
        private readonly MCPClientFactory _mcpClientFactory;
        private readonly IToolRegistry _toolRegistry;
        
        /// <summary>
        /// Models loaded event
        /// </summary>
        public event EventHandler<ModelLoadedEventArgs> ModelsLoaded;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="llmClient">LLM client</param>
        /// <param name="mcpClientFactory">MCP client factory</param>
        /// <param name="toolRegistry">Tool registry</param>
        public MCPLLMClientService(
            ILLMClient llmClient,
            MCPClientFactory mcpClientFactory,
            IToolRegistry toolRegistry)
        {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _mcpClientFactory = mcpClientFactory ?? throw new ArgumentNullException(nameof(mcpClientFactory));
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        }
        
        /// <summary>
        /// Get available models
        /// </summary>
        /// <returns>Task with model list</returns>
        public async Task<IEnumerable<string>> GetAvailableModelsAsync()
        {
        var models = await _llmClient.GetModelsAsync();
        ModelsLoaded?.Invoke(this, new ModelLoadedEventArgs(models));
            return models;
    }
        
        /// <summary>
        /// Generate text response
        /// </summary>
        /// <param name="prompt">Prompt</param>
        /// <param name="model">Model name</param>
        /// <returns>Response text</returns>
        public async Task<string> GenerateTextResponseAsync(string prompt, string model)
        {
            return await _llmClient.GenerateTextAsync(prompt, model);
        }
        
        /// <summary>
        /// Generate text response with image
        /// </summary>
        /// <param name="prompt">Prompt</param>
        /// <param name="imagePath">Image path</param>
        /// <param name="model">Model name</param>
        /// <returns>Response text</returns>
        public async Task<string> GenerateResponseWithImageAsync(string prompt, string imagePath, string model)
        {
            if (_llmClient is IVisionCapableClient visionClient)
            {
                return await visionClient.GenerateTextWithImageAsync(prompt, imagePath, model);
            }
            else
            {
                throw new NotSupportedException("Current model does not support vision capabilities");
            }
        }
        
        /// <summary>
        /// Generate streaming response
        /// </summary>
        /// <param name="prompt">Prompt</param>
        /// <param name="model">Model name</param>
        /// <returns>Response chunks</returns>
        public async IAsyncEnumerable<string> GenerateStreamResponseAsync(string prompt, string model)
        {
            await foreach (var chunk in _llmClient.GenerateStreamTextAsync(prompt, model))
            {
                yield return chunk;
            }
        }
        
        /// <summary>
        /// Generate with MCP
        /// </summary>
        /// <param name="prompt">Prompt</param>
        /// <param name="model">Model name</param>
        /// <param name="tools">Tools</param>
        /// <returns>MCP response</returns>
        public async Task<MCPResponse> GenerateWithMCPAsync(string prompt, string model, List<ToolDefinition> tools)
        {
            // Check if we have an MCP client for this model
            var mcpClient = _mcpClientFactory.GetMCPClient(model);
            
            if (mcpClient == null)
            {
                // Fall back to text response
                var text = await _llmClient.GenerateTextAsync(prompt, model);
                return new MCPResponse { Type = "text", Text = text };
            }
            
            return await mcpClient.GenerateWithMCPAsync(prompt, model, tools);
        }
        
        /// <summary>
        /// Continue with tool result
        /// </summary>
        /// <param name="originalInput">Original input</param>
        /// <param name="toolName">Tool name</param>
        /// <param name="toolResult">Tool result</param>
        /// <param name="model">Model name</param>
        /// <returns>MCP response</returns>
        public async Task<MCPResponse> ContinueWithToolResultAsync(string originalInput, string toolName, object toolResult, string model)
        {
            // Check if we have an MCP client for this model
            var mcpClient = _mcpClientFactory.GetMCPClient(model);
            
            if (mcpClient == null)
            {
                // Fall back to text response
                var prompt = $"User input: {originalInput}\n\nTool: {toolName}\nResult: {JsonSerializer.Serialize(toolResult)}\n\nPlease continue the conversation with this tool result.";
                var text = await _llmClient.GenerateTextAsync(prompt, model);
                return new MCPResponse { Type = "text", Text = text };
            }
            
            return await mcpClient.ContinueWithToolResultAsync(originalInput, toolName, toolResult, model);
        }
        
        /// <summary>
        /// Check if a model supports MCP
        /// </summary>
        /// <param name="model">Model name</param>
        /// <returns>True if MCP is supported</returns>
        public bool ModelSupportsMCP(string model)
        {
            if (string.IsNullOrEmpty(model))
                return false;
            
            return _mcpClientFactory.GetMCPClient(model) != null;
        }
    }
}
