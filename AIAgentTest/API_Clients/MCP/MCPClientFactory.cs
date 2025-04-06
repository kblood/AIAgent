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
        /// Removes an MCP server from the factory and cleans up resources
        /// </summary>
        /// <param name="serverName">Name of the server to remove</param>
        /// <returns>True if server was found and removed</returns>
        public async Task<bool> RemoveMCPServerAsync(string serverName)
        {
            System.Diagnostics.Debug.WriteLine($"Removing MCP server '{serverName}'...");
            
            if (_serverClients.TryGetValue(serverName, out var server))
            {
                try
                {
                    // First, unregister any tools associated with this server
                    if (_toolRegistry != null)
                    {
                        var allTools = _toolRegistry.GetAllTools().ToList();
                        foreach (var tool in allTools)
                        {
                            // Check if this tool belongs to the server we're removing
                            bool isServerTool = false;
                            if (tool.Metadata != null && 
                                tool.Metadata.TryGetValue("server_name", out var toolServer) && 
                                toolServer.ToString().Equals(serverName, StringComparison.OrdinalIgnoreCase))
                            {
                                isServerTool = true;
                            }
                            
                            if (isServerTool)
                            {
                                // Disable the tool first (this will stop it from being used)
                                _toolRegistry.DisableTool(tool.Name);
                                System.Diagnostics.Debug.WriteLine($"Disabled tool '{tool.Name}' from server '{serverName}'");
                                
                                // We would ideally remove the tool completely, but the interface doesn't 
                                // have a method for that, so disabling is the best we can do
                            }
                        }
                    }
                    
                    // Then, stop the server
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"Stopping server '{serverName}'...");
                        server.StopServer();
                        System.Diagnostics.Debug.WriteLine($"Server '{serverName}' stopped successfully");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error stopping server '{serverName}': {ex.Message}");
                        // Continue with removal even if stop fails
                    }
                    
                    // Finally, dispose the server if it's disposable
                    try
                    {
                        if (server is IDisposable disposable)
                        {
                            System.Diagnostics.Debug.WriteLine($"Disposing server '{serverName}'...");
                            disposable.Dispose();
                            System.Diagnostics.Debug.WriteLine($"Server '{serverName}' disposed");
                        }
                        else if (server is IAsyncDisposable asyncDisposable)
                        {
                            System.Diagnostics.Debug.WriteLine($"Async disposing server '{serverName}'...");
                            await asyncDisposable.DisposeAsync();
                            System.Diagnostics.Debug.WriteLine($"Server '{serverName}' async disposed");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error disposing server '{serverName}': {ex.Message}");
                        // Continue with removal even if dispose fails
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during cleanup for server '{serverName}': {ex.Message}");
                    // Continue with removal even if cleanup fails
                }
                
                // Remove the server from our dictionary
                _serverClients.Remove(serverName);
                System.Diagnostics.Debug.WriteLine($"Server '{serverName}' removed from registry");
                
                return true;
            }
            
            System.Diagnostics.Debug.WriteLine($"Server '{serverName}' not found in registry");
            return false;
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
                // Using Task.Run to avoid thread blocking in the UI
                Task.Run(async () =>
                {
                    try
                    {
                        // Log what we're doing
                        System.Diagnostics.Debug.WriteLine($"Preloading tools for server '{serverName}'...");
                        
                        // Try to start the server if needed
                        bool isAvailable = false;
                        try
                        {
                            isAvailable = await serverClient.IsAvailableAsync();
                            System.Diagnostics.Debug.WriteLine($"Server '{serverName}' is available: {isAvailable}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error checking if server '{serverName}' is available: {ex.Message}");
                        }
                        
                        if (!isAvailable)
                        {
                            try
                            {
                                System.Diagnostics.Debug.WriteLine($"Starting server '{serverName}'...");
                                isAvailable = await serverClient.StartServerAsync();
                                System.Diagnostics.Debug.WriteLine($"Started server '{serverName}': {isAvailable}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error starting server '{serverName}': {ex.Message}");
                            }
                        }
                        
                        // Get tools from the server with a timeout
                        List<Services.MCP.ToolDefinition> tools = null;
                        try
                        {
                            // Use a cancellation token with a reasonable timeout
                            var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
                            var getToolsTask = serverClient.GetToolsAsync();
                            
                            // Wait for the task to complete or timeout
                            await Task.WhenAny(
                                getToolsTask, 
                                Task.Delay(3000, cts.Token));
                                //Task.Delay(30000, cts.Token));

                if (getToolsTask.IsCompleted)
                            {
                                cts.Cancel(); // Cancel the delay task
                                tools = await getToolsTask; // Get the result
                                System.Diagnostics.Debug.WriteLine($"Retrieved {tools?.Count ?? 0} tools from server '{serverName}'");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Timed out waiting for tools from server '{serverName}'");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error getting tools from server '{serverName}': {ex.Message}");
                        }
                        
                        // Register tools if we got them and have a registry
                        if (_toolRegistry != null && tools != null && tools.Count > 0)
                        {
                            int registeredCount = 0;
                            foreach (var tool in tools)
                            {
                                try
                                {
                                    // Skip null tools
                                    if (tool == null)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Skipping null tool from server '{serverName}'");
                                        continue;
                                    }
                                    
                                    // Skip tools with no name
                                    if (string.IsNullOrEmpty(tool.Name))
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Skipping tool with no name from server '{serverName}'");
                                        continue;
                                    }
                                    
                                    // Add server metadata if missing
                                    if (tool.Metadata == null)
                                    {
                                        tool.Metadata = new Dictionary<string, object>();
                                    }
                                    
                                    if (!tool.Metadata.ContainsKey("server_name"))
                                    {
                                        tool.Metadata["server_name"] = serverName;
                                    }
                                    
                                    // Add server type if missing
                                    if (!tool.Metadata.ContainsKey("server_type"))
                                    {
                                        tool.Metadata["server_type"] = "filesystem";
                                    }
                                    
                                    // Register tool with handler that delegates to the server
                                    try
                                    {
                                        // Create the tool handler closure with a reference to the server
                                        // This is critical for tools to work properly
                                        var currentServer = serverClient; // Capture the current server in the closure
                                        var currentToolName = tool.Name; // Capture the current tool name in the closure
                                        
                                        // Ensure unique wrapper for delegates
                                        var toolHandler = new Func<object, Task<object>>(async (input) =>
                                        {
                                            try
                                            {
                                                // Log the execution
                                                System.Diagnostics.Debug.WriteLine($"Executing tool '{currentToolName}' from server '{serverName}'...");
                                                
                                                // Execute the tool, relying on the server's error handling
                                                var result = await currentServer.ExecuteToolAsync(currentToolName, input);
                                                
                                                // Log the success
                                                System.Diagnostics.Debug.WriteLine($"Successfully executed tool '{currentToolName}' from server '{serverName}'");
                                                
                                                return result;
                                            }
                                            catch (Exception ex)
                                            {
                                                // Log the error
                                                System.Diagnostics.Debug.WriteLine($"Error executing tool '{currentToolName}' from server '{serverName}': {ex.Message}");
                                                
                                                // Return a structured error object
                                                return new { error = $"Error executing tool '{currentToolName}': {ex.Message}" };
                                            }
                                        });
                                        
                                        // Register the tool with our handler
                                        _toolRegistry.RegisterTool(tool, toolHandler);
                                        registeredCount++;
                                        
                                        System.Diagnostics.Debug.WriteLine($"Registered tool '{tool.Name}' from server '{serverName}'");
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error registering tool '{tool.Name}' from server '{serverName}': {ex.Message}");
                                    }
                                }
                                catch (Exception toolEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error processing tool from server '{serverName}': {toolEx.Message}");
                                }
                            }
                            
                            System.Diagnostics.Debug.WriteLine($"Successfully registered {registeredCount}/{tools.Count} tools from server '{serverName}'");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"No tools available from server '{serverName}' or no tool registry available");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log but don't fail - this is just preloading
                        System.Diagnostics.Debug.WriteLine($"Error preloading tools for server '{serverName}': {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
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