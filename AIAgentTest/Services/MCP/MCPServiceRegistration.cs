using AIAgentTest.API_Clients;
using AIAgentTest.API_Clients.MCP;
using AIAgentTest.Services.Interfaces;
using System.Threading.Tasks;
using System;
using System.IO;
using AIAgentTest.ViewModels;
using IDebugLogger = AIAgentTest.Services.Interfaces.IDebugLogger;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Extension methods for registering MCP services
    /// </summary>
    public static class MCPServiceRegistration
    {
        /// <summary>
        /// Register an HTTP-based MCP server with the default factory
        /// </summary>
        /// <param name="name">Server name</param>
        /// <param name="url">Server URL</param>
        /// <returns>The registered client</returns>
        public static IMCPServerClient RegisterHttpServer(string name, string url)
        {
            var logger = ServiceProvider.GetService<AIAgentTest.Services.Interfaces.IDebugLogger>();
            var mcpClientFactory = ServiceProvider.GetService<MCPClientFactory>();
            
            if (mcpClientFactory == null)
            {
                logger?.Log("MCP client factory not found, returning null");
                return null;
            }
            
            return MCPServerRegistration.RegisterHttpServer(mcpClientFactory, name, url, logger);
        }
        
        /// <summary>
        /// Register a direct HTTP MCP client with the default factory
        /// This uses the simplified client implementation that directly communicates with an MCP server
        /// </summary>
        /// <param name="name">Server name</param>
        /// <param name="url">Server URL</param>
        /// <returns>The registered client</returns>
        public static IMCPServerClient RegisterSimplifiedMCPClient(string name, string url)
        {
            var logger = ServiceProvider.GetService<AIAgentTest.Services.Interfaces.IDebugLogger>();
            var mcpClientFactory = ServiceProvider.GetService<MCPClientFactory>();
            
            if (mcpClientFactory == null)
            {
                logger?.Log("MCP client factory not found, returning null");
                return null;
            }
            
            logger?.Log($"Creating SimplifiedMCPClient for '{name}' at URL: {url}");
            
            // Add http:// prefix if missing
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = $"http://{url}";
            }
            
            var mcpClient = new SimplifiedMCPClient(url, logger);
            mcpClientFactory.RegisterMCPServer(name, mcpClient);
            logger?.Log($"Successfully registered SimplifiedMCPClient for '{name}'");
            
            return mcpClient;
        }
        
        /// <summary>
        /// Register a StdioMCP client for filesystem access with the default factory
        /// </summary>
        /// <param name="name">Server name</param>
        /// <param name="targetDirectory">Directory to provide access to</param>
        /// <returns>The registered client</returns>
        public static IMCPServerClient RegisterStdioMCPClient(string name, string targetDirectory)
        {
            var logger = ServiceProvider.GetService<AIAgentTest.Services.Interfaces.IDebugLogger>();
            var mcpClientFactory = ServiceProvider.GetService<MCPClientFactory>();
            
            if (mcpClientFactory == null)
            {
                logger?.Log("MCP client factory not found, returning null");
                return null;
            }
            
            logger?.Log($"Creating StdioMCPServerClient for '{name}' with directory {targetDirectory}");
            
            // If directory doesn't exist, create it or use a reliable system directory
            if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory))
            {
                logger?.Log($"Target directory does not exist: {targetDirectory}");
                
                // Use a reliable system directory
                targetDirectory = Path.GetTempPath();
                logger?.Log($"Using temp directory instead: {targetDirectory}");
            }

            // Create command and arguments with proper executable path
            string command = "cmd.exe";
            string[] args = new[] { "/c", "npx", "-y", "@modelcontextprotocol/server-filesystem", targetDirectory };
            //string[] args = new[] { "/c", "npx", "-y", "@modelcontextprotocol/server-filesystem", "--stdio", targetDirectory };

            // Completely skip stdio client due to stability issues
            logger?.Log("Using SimplifiedMCPClient instead of StdioMCPServerClient due to stability issues");
            return RegisterSimplifiedMCPClient(name, "http://localhost:3000");
        }
        
        /// <summary>
        /// Registers all MCP services with the service provider (non-async version)
        /// </summary>
        public static void RegisterMCPServices()
        {
            // Call the async version and wait for it to complete
            RegisterMCPServicesAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
        
        /// <summary>
        /// Registers all MCP services with the service provider
        /// </summary>
        public static async Task RegisterMCPServicesAsync()
        {
            // Register the debug logger first if it doesn't exist
            var debugViewModel = ServiceProvider.GetService<ViewModels.DebugViewModel>();
            if (debugViewModel != null)
            {
                var debugLogger = new DebugLogger(debugViewModel);
                ServiceProvider.RegisterService<AIAgentTest.Services.Interfaces.IDebugLogger>(debugLogger);
                debugLogger.Log("Registering MCP services...");
            }
            
            // Get the debug logger
            var logger = ServiceProvider.GetService<AIAgentTest.Services.Interfaces.IDebugLogger>();
            
            // Register the tool registry
            logger?.Log("Registering tool registry...");
            var toolRegistry = new ToolRegistry();
            ServiceProvider.RegisterService<IToolRegistry>(toolRegistry);
            
            // Register the MCP context manager
            logger?.Log("Registering MCP context manager...");
            var ollamaClient = (OllamaClient)LLMClientFactory.GetClient(LLMClientFactory.ProviderType.Ollama);
            var mcpContextManager = new MCPContextManager(ollamaClient);
            ServiceProvider.RegisterService<IMCPContextManager>(mcpContextManager);
            
            // Register the MCP client factory
            logger?.Log("Registering MCP client factory...");
            var llmClientFactory = new LLMClientFactory();
            var messageParsingService = ServiceProvider.GetService<AIAgentTest.Services.Interfaces.IMessageParsingService>();
            var mcpClientFactory = new MCPClientFactory(llmClientFactory, messageParsingService, toolRegistry);
            ServiceProvider.RegisterService<MCPClientFactory>(mcpClientFactory);
            
            // Register the MCP LLM client service
            logger?.Log("Registering MCP LLM client service...");
            var llmClient = LLMClientFactory.GetClient(LLMClientFactory.ProviderType.Ollama);
            var mcpLLMClientService = new MCPLLMClientService(llmClient, mcpClientFactory, toolRegistry);
            ServiceProvider.RegisterService<IMCPLLMClientService>(mcpLLMClientService);
            
            // Register common tools
            logger?.Log("Registering common tools...");
            var commonTools = new CommonTools();
            ServiceProvider.RegisterService<CommonTools>(commonTools);

            // Register tools with the registry
            logger?.Log("Registering tools with registry...");
            commonTools.RegisterCommonTools(toolRegistry);

            // Register MCP servers and properly await
            logger?.Log("Registering MCP servers...");
            try 
            {
                // First use RegisterMCPServers with skipStartup=true to defer startup
                var registeredServers = await MCPServerRegistration.RegisterMCPServersAsync(mcpClientFactory, logger, true);
                logger?.Log("MCP server registration completed.");

                logger?.Log("Registering MCPServerManagerViewModel...");
                // Assuming it needs the factory and maybe the logger:
                var uiViewModelInstance = new ViewModels.MCPServerManagerViewModel(mcpClientFactory);
                ServiceProvider.RegisterService<ViewModels.MCPServerManagerViewModel>(uiViewModelInstance);
                logger?.Log("MCPServerManagerViewModel registered.");

                // Populate UI with servers regardless of whether they can be started
                var uiViewModel = ServiceProvider.GetService<ViewModels.MCPServerManagerViewModel>();
                if (uiViewModel != null)
                {
                    // Force UI refresh
                    logger?.Log("Refreshing server UI...");
                    // Call Refresh command instead of private LoadServers method
                    if (uiViewModel.RefreshServersCommand != null && uiViewModel.RefreshServersCommand.CanExecute(null))
                    {
                        uiViewModel.RefreshServersCommand.Execute(null);
                    }
                }
                
                // Register a default client if none were registered
                var servers = mcpClientFactory.GetRegisteredServerNames();
                if (servers == null || servers.Count == 0)
                {
                    logger?.Log("No MCP servers registered. Adding a default MCP server");
                    
                    // Use temp directory which should always exist with proper permissions
                    string targetDir = Path.GetTempPath();
                    logger?.Log($"Using target directory: {targetDir}");
                    
                    // Create and register the simplified client instead of stdio
                    logger?.Log("Using SimplifiedMCPClient instead of StdioMCPServerClient due to potential stability issues");
                    var mcpClient = new SimplifiedMCPClient("http://localhost:3000", logger);
                    mcpClientFactory.RegisterMCPServer("FileServer", mcpClient);
                    logger?.Log("Adding FileServer to UI manually");
                    
                    // Update UI to show the default server
                    if (uiViewModel != null)
                    {
                        logger?.Log("Refreshing server UI after adding default server...");
                        // Call Refresh command instead of private LoadServers method
                        if (uiViewModel.RefreshServersCommand != null && uiViewModel.RefreshServersCommand.CanExecute(null))
                        {
                            uiViewModel.RefreshServersCommand.Execute(null);
                        }
                    }
                }
                else
                {
                    logger?.Log($"Found {servers.Count} registered MCP servers: {string.Join(", ", servers)}");
                }
                
                // Register tools from MCP servers without trying to start them yet
                logger?.Log("About to register server tools...");
                try {
                    await MCPServerToolRegistration.RegisterServerToolsAsync(toolRegistry, mcpClientFactory, logger);
                    logger?.Log("Server tool registration completed successfully.");
                } catch (Exception toolEx) {
                    logger?.Log($"Error during tool registration: {toolEx.Message}");
                    logger?.Log($"Stack trace: {toolEx.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                logger?.Log($"Error during MCP server registration: {ex.Message}");
                logger?.Log($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}