using System;
using System.Collections.Generic;
using System.Linq;
using AIAgentTest.Services.Interfaces;
using AIAgentTest.Services.MCP;
using System.Threading.Tasks;

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
        public IMCPLLMClient GetMCPClient(string modelName) => GetClientForModel(modelName);

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
                modelName.StartsWith("qwen") ||
                modelName.StartsWith("acidtib/qwen2.5-coder-cline:7b"))
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
        /// <param name="loadToolsImmediately">Whether to attempt to load tools immediately</param>
        public void RegisterMCPServer(string serverName, IMCPServerClient serverClient, bool loadToolsImmediately = true)
        {
            // Store the client
            _serverClients[serverName] = serverClient;
            
            // Optionally initiate tool loading in the background
            if (loadToolsImmediately)
            {
                // Fire and forget - we don't want to block the registration process
                Task.Run(async () =>
                {
                    try
                    {
                        // Try to start the server and get tools
                        var isAvailable = await serverClient.IsAvailableAsync();
                        if (!isAvailable)
                        {
                            await serverClient.StartServerAsync();
                        }
                        
                        // Get tools from the server
                        var tools = await serverClient.GetToolsAsync();
                        
                        // Register tools if we have a registry
                        if (_toolRegistry != null && tools != null && tools.Count > 0)
                        {
                            foreach (var tool in tools)
                            {
                                // Add server metadata if missing
                                if (tool.Metadata == null)
                                {
                                    tool.Metadata = new Dictionary<string, object>();
                                }
                                
                                if (!tool.Metadata.ContainsKey("server_name"))
                                {
                                    tool.Metadata["server_name"] = serverName;
                                }
                                
                                // Register tool with handler that delegates to the server
                                _toolRegistry.RegisterTool(tool, async (input) =>
                                {
                                    return await serverClient.ExecuteToolAsync(tool.Name, input);
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail - this is just preloading
                        System.Diagnostics.Debug.WriteLine($"Error preloading tools for server '{serverName}': {ex.Message}");
                    }
                });
            }
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
        
        /// <summary>
        /// Gets all registered server names
        /// </summary>
        /// <returns>List of registered server names</returns>
        public List<string> GetAllRegisteredServers()
        {
            return _serverClients.Keys.ToList();
        }
        
        /// <summary>
        /// Get a list of all registered MCP server names
        /// </summary>
        /// <returns>List of server names</returns>
        public List<string> GetRegisteredServerNames()
        {
            return _serverClients.Keys.ToList();
        }

        /// <summary>
        /// Get server configurations as a dictionary
        /// </summary>
        /// <returns>Dictionary of server configurations</returns>
        public Dictionary<string, IMCPServerClient> GetRegisteredServers()
        {
            return new Dictionary<string, IMCPServerClient>(_serverClients);
        }
        
        /// <summary>
        /// Register a new StdioMCPServerClient with the factory
        /// </summary>
        /// <param name="serverName">Name for the server (e.g., "FileServer")</param>
        /// <param name="command">Command to run (e.g., "npx")</param>
        /// <param name="arguments">Arguments for the command</param>
        /// <param name="workingDirectory">Optional working directory</param>
        /// <param name="logger">Optional debug logger</param>
        /// <returns>The registered client</returns>
        public IMCPServerClient RegisterStdioMCPServer(string serverName, string command, string[] arguments, 
            string workingDirectory = null, IDebugLogger logger = null)
        {
            // Standardize server name
            if (serverName.Equals("fileserver", StringComparison.OrdinalIgnoreCase))
            {
                serverName = "FileServer";  // Use consistent capitalization
            }
            
            // Create the StdioMCPServerClient
            var mcpClient = new StdioMCPServerClient(command, arguments, workingDirectory, logger);
            
            // Register it with the factory
            RegisterMCPServer(serverName, mcpClient);
            
            return mcpClient;
        }
        
        /// <summary>
        /// Register a new StdioMCPServerClient for the filesystem server
        /// </summary>
        /// <param name="targetDirectory">Directory to provide access to</param>
        /// <param name="logger">Optional debug logger</param>
        /// <returns>The registered client</returns>
        public IMCPServerClient RegisterFilesystemStdioServer(string targetDirectory, IDebugLogger logger = null)
        {
            // Build command and arguments
            string command = "npx";
            var argsList = new List<string> { "-y", "@modelcontextprotocol/server-filesystem", "--stdio" };
            
            // Add target directory if specified
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                argsList.Add(targetDirectory);
            }
            
            // Create and register the client
            return RegisterStdioMCPServer("FileServer", command, argsList.ToArray(), targetDirectory, logger);
        }
    }
}
