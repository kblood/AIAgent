using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AIAgentTest.API_Clients.MCP;
using AIAgentTest.Services.Interfaces;
using System.Linq;
using System.Threading;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Helper for registering MCP servers
    /// </summary>
    public static class MCPServerRegistration
    {
        /// <summary>
        /// MCP Server configuration class
        /// </summary>
        public class MCPServerConfig
        {
            [JsonPropertyName("command")]
            public string Command { get; set; }
            
            [JsonPropertyName("args")]
            public string[] Args { get; set; }
            
            [JsonIgnore]
            public bool IsEnabled { get; set; } = true;
        }
        
        /// <summary>
        /// MCP Servers configuration class
        /// </summary>
        public class MCPServersConfig
        {
            [JsonPropertyName("mcpServers")]
            public Dictionary<string, MCPServerConfig> McpServers { get; set; } = new Dictionary<string, MCPServerConfig>();
        }

        /// <summary>
        /// Register MCP servers from settings
        /// </summary>
        /// <param name="mcpClientFactory">MCP client factory</param>
        /// <param name="logger">Debug logger</param>
        /// <param name="skipStartup">Whether to skip starting the servers during registration</param>
        /// <returns>Task with the number of registered servers</returns>
        public static async Task<int> RegisterMCPServersAsync(MCPClientFactory mcpClientFactory, IDebugLogger logger = null, bool skipStartup = false)
        {
            if (mcpClientFactory == null)
                throw new ArgumentNullException(nameof(mcpClientFactory));
            
            int registeredCount = 0;
            logger?.Log("Loading MCP server configurations");

            // First check the mcp.json file in .roo directory
            string mcpJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".roo", "mcp.json");
            logger?.Log($"Checking for mcp.json at: {mcpJsonPath}");

            if (File.Exists(mcpJsonPath))
            {
                logger?.Log($"Found mcp.json at: {mcpJsonPath}");
                try
                {
                    string jsonContent = await File.ReadAllTextAsync(mcpJsonPath);
                    logger?.Log("Successfully read mcp.json content");
                    logger?.Log($"Content: {jsonContent}");

                    // Use options with case-insensitive property names
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };
            
            // Removed erroneous return statement
                    
                    var serversConfig = JsonSerializer.Deserialize<MCPServersConfig>(jsonContent, options);
                    if (serversConfig?.McpServers != null)
                    {
                        logger?.Log($"Found {serversConfig.McpServers.Count} server configurations in mcp.json");
                        
                        foreach (var serverEntry in serversConfig.McpServers)
                        {
                            try
                            {
                                var name = serverEntry.Key;
                                var config = serverEntry.Value;
                                
                                logger?.Log($"Registering server '{name}' from mcp.json");
                                logger?.Log($"Command: {config.Command}, Args: {string.Join(" ", config.Args)}");
                                
                                // Register server with debug logger
                                await RegisterServerFromConfig(mcpClientFactory, name, config, logger);
                                registeredCount++;
                            }
                            catch (Exception ex)
                            {
                                logger?.Log($"Error registering MCP server {serverEntry.Key}: {ex.Message}, but continuing with next server");
                            }
                        }
                        
                        // Save to settings for future reference
                        Properties.Settings.Default.MCPServers = jsonContent;
                        Properties.Settings.Default.Save();
                        
                        return registeredCount;
                    }
                }
                catch (Exception ex)
                {
                    logger?.Log($"Error processing mcp.json: {ex.Message}");
                    logger?.Log($"Exception details: {ex}");
                }
            }
            else
            {
                logger?.Log($"mcp.json not found at path: {mcpJsonPath}");
            }
            
            // Fall back to settings if mcp.json failed
            var serversString = Properties.Settings.Default.MCPServers;
            if (string.IsNullOrEmpty(serversString))
            {
                logger?.Log("No MCP server configurations found in settings");
                return registeredCount;
            }
            
            logger?.Log("Using MCP server configurations from settings");
            
            // Try to parse the server config as JSON first (ModelContextProtocol standard)
            try
            {
                // Use options with case-insensitive property names
                var options = new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                
                var serversConfig = JsonSerializer.Deserialize<MCPServersConfig>(serversString, options);
                if (serversConfig?.McpServers != null)
                {
                    // Get enabled servers from settings
                    var enabledServers = new HashSet<string>();
                    var disabledServers = new HashSet<string>();
                    
                    var enabledServersStr = Properties.Settings.Default.EnabledMCPServers;
                    logger?.Log($"Enabled MCP servers from settings: {enabledServersStr}");
                    
                    if (!string.IsNullOrEmpty(enabledServersStr))
                    {
                        var serverEntries = enabledServersStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var server in serverEntries)
                        {
                            if (server.StartsWith("!"))
                            {
                                disabledServers.Add(server.Substring(1));
                                logger?.Log($"Server '{server.Substring(1)}' explicitly disabled");
                            }
                            else
                            {
                                enabledServers.Add(server);
                                logger?.Log($"Server '{server}' explicitly enabled");
                            }
                        }
                    }
                    
                    logger?.Log($"Found {serversConfig.McpServers.Count} server configurations in settings");
                    
                    foreach (var serverEntry in serversConfig.McpServers)
                    {
                        try
                        {
                            var name = serverEntry.Key;
                            var config = serverEntry.Value;
                            
                            logger?.Log($"Processing server '{name}' from settings");
                            
                            // Check if explicitly enabled/disabled
                            if (enabledServers.Count > 0 || disabledServers.Count > 0)
                            {
                                config.IsEnabled = !disabledServers.Contains(name) && 
                                              (enabledServers.Count == 0 || enabledServers.Contains(name));
                            }
                            
                            if (!config.IsEnabled)
                            {
                                logger?.Log($"Server '{name}' is disabled, skipping");
                                continue;
                            }
                                
                            logger?.Log($"Registering server '{name}' from settings");
                            logger?.Log($"Command: {config.Command}, Args: {string.Join(" ", config.Args)}");
                            
                            await RegisterServerFromConfig(mcpClientFactory, name, config, logger);
                                registeredCount++;
                        }
                        catch (Exception ex)
                        {
                            logger?.Log($"Error registering MCP server {serverEntry.Key}: {ex.Message}, but continuing with next server");
                        }
                    }
                    
                    // Successfully parsed JSON config, no need to try the legacy format
                    return registeredCount;
                }
            }
            catch (Exception ex)
            {
                // Not valid JSON, try legacy format
                logger?.Log($"Error parsing settings as JSON: {ex.Message}, trying legacy format");
            }
            
            // Legacy format: name|url|type|enabled
            logger?.Log("Attempting to parse servers string in legacy format (name|url|type|enabled)");
            var serversList = serversString.Split(';');
            logger?.Log($"Found {serversList.Length} server entries in legacy format");
            
            foreach (var serverString in serversList.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                try
                {
                    logger?.Log($"Processing server entry: {serverString}");
                    var parts = serverString.Split('|');
                    if (parts.Length < 3)
                    {
                        logger?.Log("Invalid server entry format (need at least 3 parts)");
                        continue;
                    }
                    
                    var name = parts[0];
                    var url = parts[1];
                    var type = parts[2];
                    var isEnabled = parts.Length > 3 && bool.TryParse(parts[3], out bool enabled) && enabled;
                    
                    logger?.Log($"Server '{name}' of type '{type}' at '{url}' is {(isEnabled ? "enabled" : "disabled")}");
                    
                    if (!isEnabled)
                    {
                        logger?.Log($"Server '{name}' is disabled, skipping");
                        continue;
                    }
                    
                    // Register the server based on type
                    switch (type.ToLowerInvariant())
                    {
                        case "filesystem":
                            logger?.Log($"Setting up filesystem server '{name}' with URL {url}");
                            // Use the HTTP client approach
                            RegisterHttpServer(mcpClientFactory, name, url, logger);
                            registeredCount++;
                            break;
                            
                        default:
                            logger?.Log($"Unsupported server type: {type}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    logger?.Log($"Error registering MCP server: {ex.Message}, but continuing with next server");
                }
            }
            
            // Make sure we return the registered count in all cases
            return registeredCount;
        }
        
        /// <summary>
        /// Register an HTTP-based MCP server
        /// </summary>
        /// <param name="mcpClientFactory">MCP client factory</param>
        /// <param name="name">Server name</param>
        /// <param name="url">Server URL</param>
        /// <param name="logger">Debug logger</param>
        /// <returns>The registered client</returns>
        public static IMCPServerClient RegisterHttpServer(MCPClientFactory mcpClientFactory, string name, string url, IDebugLogger logger = null)
        {
            logger?.Log($"Registering HTTP MCP server '{name}' at URL {url}");
            
            // Standardize server name
            string serverName = name;
            if (serverName.Equals("fileserver", StringComparison.OrdinalIgnoreCase))
            {
                serverName = "FileServer";  // Use consistent capitalization
                logger?.Log($"Standardizing server name from '{name}' to '{serverName}'");
            }
            
            // Add http:// prefix if missing
            string serverUrl = url;
            if (!serverUrl.StartsWith("http://") && !serverUrl.StartsWith("https://"))
            {
                serverUrl = $"http://{serverUrl}";
            }
            
            // Create and register the client
            logger?.Log($"Creating SimplifiedMCPClient for '{serverName}' at URL: {serverUrl}");
            var mcpClient = new SimplifiedMCPClient(serverUrl, logger);
            mcpClientFactory.RegisterMCPServer(serverName, mcpClient);
            logger?.Log($"Successfully registered SimplifiedMCPClient for '{serverName}'");
            
            return mcpClient;
        }
        
        /// <summary>
        /// Register a StdioMCP server for filesystem access
        /// </summary>
        /// <param name="mcpClientFactory">MCP client factory</param>
        /// <param name="name">Server name</param>
        /// <param name="targetDirectory">Directory to provide access to</param>
        /// <param name="logger">Debug logger</param>
        /// <returns>The registered client</returns>
        public static async Task<IMCPServerClient> RegisterStdioFilesystemServerAsync(
            MCPClientFactory mcpClientFactory, 
            string name, 
            string targetDirectory, 
            IDebugLogger logger = null)
        {
            logger?.Log($"Registering Stdio MCP server '{name}' for directory {targetDirectory}");
            
            // Standardize server name
            string serverName = name;
            if (serverName.Equals("fileserver", StringComparison.OrdinalIgnoreCase))
            {
                serverName = "FileServer";  // Use consistent capitalization
                logger?.Log($"Standardizing server name from '{name}' to '{serverName}'");
            }
            
            // Create the command and arguments
            string command = "npx";
            var arguments = new List<string> { "-y", "@modelcontextprotocol/server-filesystem", "--stdio" };
            
            // Add target directory if specified
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                arguments.Add(targetDirectory);
            }
            
            // Create the client
            logger?.Log($"Creating StdioMCPServerClient for '{serverName}' with command {command} {string.Join(" ", arguments)}");
            var mcpClient = new StdioMCPServerClient(command, arguments.ToArray(), targetDirectory, logger);
            
            // Start the server to ensure it's working
            bool started = await mcpClient.StartServerAsync();
            if (!started)
            {
                logger?.Log($"Failed to start StdioMCPServerClient for '{serverName}'");
                mcpClient.Dispose();
                return null;
            }
            
            // Register the client
            mcpClientFactory.RegisterMCPServer(serverName, mcpClient);
            logger?.Log($"Successfully registered StdioMCPServerClient for '{serverName}'");
            
            return mcpClient;
        }

        /// <summary>
        /// Register a server from its configuration
        /// </summary>
        /// <param name="mcpClientFactory">MCP client factory</param>
        /// <param name="name">Server name</param>
        /// <param name="config">Server configuration</param>
        /// <param name="logger">Debug logger</param>
        /// <returns>Task representing the async operation</returns>
        private static async Task RegisterServerFromConfig(MCPClientFactory mcpClientFactory, string name, MCPServerConfig config, IDebugLogger logger = null)
        {
            logger?.Log($"Registering server '{name}' with config: {config.Command} {string.Join(" ", config.Args)}");
            
            // Ensure consistent case for server name
            string serverName = name;
            
            // Server names should be treated case-insensitively
            if (serverName.Equals("fileserver", StringComparison.OrdinalIgnoreCase))
            {
                serverName = "FileServer";  // Use consistent capitalization
                logger?.Log($"Standardizing server name from '{name}' to '{serverName}'");
            }
            
            if (string.Equals(config.Command, "npx", StringComparison.OrdinalIgnoreCase) &&
                config.Args != null && config.Args.Length > 0 && 
                config.Args.Any(arg => arg.Contains("@modelcontextprotocol/server-filesystem")))
            {
                // Check for communication mode in args
                bool usesStdio = config.Args.Contains("--stdio");

                // Set this to true anyway, because it should be the correct way to run it.
                usesStdio = true;
                bool usesHttp = config.Args.Contains("--http");
                string serverUrl = null;
                string targetDir = null;
                
                if (config.Args.Length > 0)
                {
                    // Last argument might be the target directory
                    targetDir = config.Args[config.Args.Length - 1];
                    // Check if --url or -u is specified
                    for (int i = 0; i < config.Args.Length - 1; i++)
                    {
                        if ((config.Args[i] == "--url" || config.Args[i] == "-u") && 
                            i + 1 < config.Args.Length)
                        {
                            serverUrl = config.Args[i + 1];
                            break;
                        }
                    }
                }
                
                // Determine if we should use the StdioMCPServerClient based on the command arguments
                if (usesStdio)
                {
                    // Use StdioMCPServerClient for stdio mode
                    logger?.Log($"Creating StdioMCPServerClient for '{serverName}' with command {config.Command}");
                    var mcpClient = new StdioMCPServerClient(config.Command, config.Args, targetDir, logger);
                    
                    // Start the server properly with await
                    bool started = await mcpClient.StartServerAsync();
                    
                    if (!started)
                    {
                        logger?.Log($"Failed to start StdioMCPServerClient for '{serverName}'");
                        mcpClient.Dispose();
                        
                        // Fallback to HTTP if stdio fails
                        logger?.Log("Falling back to HTTP mode...");
                        
                        // Default URL if not specified
                        if (string.IsNullOrEmpty(serverUrl))
                        {
                            serverUrl = "http://localhost:3000";
                        }
                        
                        // Create a new args array with the --http flag added if needed
                        var newArgs = config.Args;
                        if (!usesHttp)
                        {
                            var argsList = config.Args.ToList();
                            int insertIndex = argsList.IndexOf("--stdio");
                            if (insertIndex >= 0)
                            {
                                argsList[insertIndex] = "--http";  // Replace --stdio with --http
                            }
                            else
                            {
                                // Find the best place to insert the flag - right after the package name
                                insertIndex = 0;
                                for (int i = 0; i < argsList.Count; i++)
                                {
                                    if (argsList[i].Contains("@modelcontextprotocol"))
                                    {
                                        insertIndex = i + 1;
                                        break;
                                    }
                                }
                                
                                // Insert --http at the appropriate position
                                argsList.Insert(insertIndex, "--http");
                            }
                            newArgs = argsList.ToArray();
                        }
                        
                        var fallbackClient = new SimplifiedMCPClient(serverUrl, config.Command, newArgs, logger);
                        mcpClientFactory.RegisterMCPServer(serverName, fallbackClient);
                        logger?.Log($"Registered fallback SimplifiedMCPClient for '{serverName}'");
                    }
                    else
                    {
                        // Successfully started stdio client
                        mcpClientFactory.RegisterMCPServer(serverName, mcpClient);
                        logger?.Log($"Successfully registered StdioMCPServerClient for '{serverName}'");
                    }
                }
                else
                {
                    // Use the HTTP client approach
                    logger?.Log($"Creating process-managing SimplifiedMCPClient for '{serverName}' with command {config.Command} and URL {serverUrl}");
                    
                    // Create a new args array with the --http flag added if needed
                    var newArgs = config.Args;
                    if (!usesHttp)
                    {
                        logger?.Log("Adding --http flag to command arguments");
                        var argsList = config.Args.ToList();
                        
                        // Find the best place to insert the flag - right after the package name
                        int insertIndex = 0;
                        for (int i = 0; i < argsList.Count; i++)
                        {
                            if (argsList[i].Contains("@modelcontextprotocol"))
                            {
                                insertIndex = i + 1;
                                break;
                            }
                        }
                        
                        // Insert --http at the appropriate position
                        argsList.Insert(insertIndex, "--http");
                        newArgs = argsList.ToArray();
                    }
                    
                    var mcpClient = new SimplifiedMCPClient(serverUrl, config.Command, newArgs, logger);
                    mcpClientFactory.RegisterMCPServer(serverName, mcpClient);
                    logger?.Log($"Successfully registered process-managing SimplifiedMCPClient for '{serverName}'");
                }
            }
            // Check if the command is "http" or "https" which means it's just an HTTP URL
            else if ((string.Equals(config.Command, "http", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(config.Command, "https", StringComparison.OrdinalIgnoreCase)) && 
                     config.Args != null && config.Args.Length > 0)
            {
                // Override server config to use stdio instead
                logger?.Log("HTTP URL command detected. Using StdioMCPServerClient instead of HTTP client.");
                
                // Create command and args for stdio
                string command = "npx";
                string[] args = new[] { "-y", "@modelcontextprotocol/server-filesystem", "--stdio", "C:\\" };
                
                // Create the client
                var mcpClient = new StdioMCPServerClient(command, args, "C:\\", logger);
                
                // Start the server properly with await
                bool started = await mcpClient.StartServerAsync();
                
                if (!started)
                {
                    logger?.Log($"Failed to start StdioMCPServerClient for '{serverName}'");
                    mcpClient.Dispose();
                    
                    // Fallback to HTTP if stdio fails
                    string serverUrl = config.Args[0];
                    if (!serverUrl.StartsWith("http://") && !serverUrl.StartsWith("https://"))
                    {
                        serverUrl = $"http://{serverUrl}";
                    }
                    
                    logger?.Log($"Falling back to HTTP server at {serverUrl}");
                    var httpClient = new SimplifiedMCPClient(serverUrl, logger);
                    mcpClientFactory.RegisterMCPServer(serverName, httpClient);
                    logger?.Log($"Registered fallback SimplifiedMCPClient for '{serverName}'");
                }
                else
                {
                    // Successfully started stdio client
                    mcpClientFactory.RegisterMCPServer(serverName, mcpClient);
                    logger?.Log($"Successfully registered StdioMCPServerClient for '{serverName}'");
                }
            }
            else
            {
                logger?.Log($"Unsupported server configuration for '{serverName}'");
            }
        }
    }
}
