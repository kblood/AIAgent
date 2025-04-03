using System;
using System.Collections.Generic;
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
        private readonly IToolRegistry _toolRegistry;
        private readonly Dictionary<string, IMCPServerClient> _serverClients = new Dictionary<string, IMCPServerClient>();
        
        /// <summary>
        /// Creates a new MCPClientFactory
        /// </summary>
        /// <param name="llmClientFactory">Factory for creating base LLM clients</param>
        /// <param name="parsingService">Service for parsing messages</param>
        /// <param name="toolRegistry">Registry of available tools</param>
        public MCPClientFactory(
            LLMClientFactory llmClientFactory,
            IMessageParsingService parsingService,
            IToolRegistry toolRegistry = null)
        {
            _llmClientFactory = llmClientFactory ?? throw new ArgumentNullException(nameof(llmClientFactory));
            _parsingService = parsingService ?? throw new ArgumentNullException(nameof(parsingService));
            _toolRegistry = toolRegistry; // No fallback for now - must be provided
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
                    
                    // Create the MCP adapter
                    var mcpAdapter = new OllamaMCPAdapter(ollamaClient, _parsingService, _toolRegistry);
                    
                    // Register any existing MCP servers
                    foreach (var entry in _serverClients)
                    {
                        mcpAdapter.RegisterMCPServer(entry.Key, entry.Value);
                    }
                    
                    return mcpAdapter;
                    
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
                modelName.StartsWith("phi") ||
                modelName.StartsWith("gemma") ||
                modelName.StartsWith("qwen"))
            {
                return CreateMCPClient("ollama");
            }
            
            // Add other model detection as needed
            
            throw new NotSupportedException($"Model {modelName} is not supported for MCP");
        }
        
        /// <summary>
        /// Registers an MCP server with the factory
        /// </summary>
        /// <param name="serverName">Name of the server</param>
        /// <param name="serverClient">Client for communicating with the server</param>
        public void RegisterMCPServer(string serverName, IMCPServerClient serverClient)
        {
            _serverClients[serverName] = serverClient;
        }

        /// <summary>
        /// Gets an MCP server by name
        /// </summary>
        /// <param name="serverName">Name of the server</param>
        /// <returns>MCP server client or null if not found</returns>
        public IMCPServerClient GetMCPServer(string serverName)
        {
            return _serverClients.TryGetValue(serverName, out var server) ? server : null;
        }
    }
}
