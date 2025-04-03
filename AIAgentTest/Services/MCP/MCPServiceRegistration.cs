using AIAgentTest.API_Clients;
using AIAgentTest.API_Clients.MCP;
using AIAgentTest.Services.Interfaces;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Extension methods for registering MCP services
    /// </summary>
    public static class MCPServiceRegistration
    {
        /// <summary>
        /// Registers all MCP services with the service provider
        /// </summary>
        public static void RegisterMCPServices()
        {
            // Register the tool registry
            var toolRegistry = new ToolRegistry();
            ServiceProvider.RegisterService<IToolRegistry>(toolRegistry);
            
            // Register the MCP context manager
            var ollamaClient = (OllamaClient)LLMClientFactory.GetClient(LLMClientFactory.ProviderType.Ollama);
            var mcpContextManager = new MCPContextManager(ollamaClient);
            ServiceProvider.RegisterService<IMCPContextManager>(mcpContextManager);
            
            // Register the MCP client factory
            var llmClientFactory = new LLMClientFactory();
            var messageParsingService = ServiceProvider.GetService<IMessageParsingService>();
            var mcpClientFactory = new MCPClientFactory(llmClientFactory, messageParsingService, toolRegistry);
            ServiceProvider.RegisterService<MCPClientFactory>(mcpClientFactory);
            
            // Register the MCP LLM client service
            var llmClient = LLMClientFactory.GetClient(LLMClientFactory.ProviderType.Ollama);
            var mcpLLMClientService = new MCPLLMClientService(llmClient, mcpClientFactory, toolRegistry);
            ServiceProvider.RegisterService<IMCPLLMClientService>(mcpLLMClientService);
            
            // Register common tools
            var commonTools = new CommonTools();
            ServiceProvider.RegisterService<CommonTools>(commonTools);
            
            // Register tools with the registry
            commonTools.RegisterCommonTools(toolRegistry);
            
            // Register MCP servers
            MCPServerRegistration.RegisterMCPServersAsync(mcpClientFactory).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Creates an enhanced Ollama MCP adapter
        /// </summary>
        public static EnhancedOllamaMCPAdapter CreateEnhancedOllamaAdapter()
        {
            var ollamaClient = (OllamaClient)LLMClientFactory.GetClient(LLMClientFactory.ProviderType.Ollama);
            var messageParsingService = ServiceProvider.GetService<IMessageParsingService>();
            var toolRegistry = ServiceProvider.GetService<IToolRegistry>();
            
            return new EnhancedOllamaMCPAdapter(ollamaClient, messageParsingService, toolRegistry);
        }
    }
}
