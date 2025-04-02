using System;
using AIAgentTest.Services.Interfaces;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.API_Clients.MCP
{
    /// <summary>
    /// Factory for creating MCP-capable LLM clients
    /// </summary>
    public class MCPClientFactory
    {
        private readonly LLMClientFactory _llmClientFactory;
        private readonly IMessageParsingService _parsingService;
        
        /// <summary>
        /// Creates a new MCPClientFactory
        /// </summary>
        /// <param name="llmClientFactory">Factory for creating base LLM clients</param>
        /// <param name="parsingService">Service for parsing messages</param>
        public MCPClientFactory(
            LLMClientFactory llmClientFactory,
            IMessageParsingService parsingService)
        {
            _llmClientFactory = llmClientFactory ?? throw new ArgumentNullException(nameof(llmClientFactory));
            _parsingService = parsingService ?? throw new ArgumentNullException(nameof(parsingService));
        }
        
        /// <summary>
        /// Creates an MCP-capable client for the specified provider
        /// </summary>
        /// <param name="provider">Provider name (e.g., "ollama", "openai")</param>
        /// <returns>MCP-capable LLM client</returns>
        public IMCPLLMClient CreateMCPClient(string provider)
        {
            switch (provider.ToLower())
            {
                case "ollama":
                    var ollamaClient = (OllamaClient)LLMClientFactory.GetClient(LLMClientFactory.ProviderType.Ollama);
                    if (ollamaClient == null)
                    {
                        throw new InvalidOperationException("Failed to create Ollama client");
                    }
                    
                    return new OllamaMCPAdapter(ollamaClient, _parsingService);
                    
                // Add other providers as needed
                    
                default:
                    throw new NotSupportedException($"Provider {provider} is not supported for MCP");
            }
        }
        
        /// <summary>
        /// Gets an MCP-capable client for the specified model
        /// </summary>
        /// <param name="modelName">Model name</param>
        /// <returns>MCP-capable LLM client</returns>
        public IMCPLLMClient GetClientForModel(string modelName)
        {
            // Simple provider detection based on model name prefixes
            if (modelName.StartsWith("llama") || 
                modelName.StartsWith("mistral") || 
                modelName.StartsWith("mixtral") ||
                modelName.StartsWith("phi"))
            {
                return CreateMCPClient("ollama");
            }
            
            // Add other model detection as needed
            
            throw new NotSupportedException($"Model {modelName} is not supported for MCP");
        }
    }
}