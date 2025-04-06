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
    /// Helper for registering MCP servers - Fixed RegisterMCPServersAsync method
    /// </summary>
    public static partial class MCPServerRegistration
    {
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
                    var options = new JsonSerializerOptions {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };
                    
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
                                RegisterServerWithFixedParams(mcpClientFactory, name, config, logger, skipStartup);
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
                            
                            RegisterServerWithFixedParams(mcpClientFactory, name, config, logger, skipStartup);
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
            
            return registeredCount;
        }
        
        /// <summary>
        /// Register a server from its configuration with fixed parameters
        /// </summary>
        /// <param name="mcpClientFactory">MCP client factory</param>
        /// <param name="name">Server name</param>
        /// <param name="config">Server configuration</param>
        /// <param name="logger">Debug logger</param>
        /// <param name="skipStartup">Whether to skip starting the servers during registration</param>
        private static void RegisterServerWithFixedParams(MCPClientFactory mcpClientFactory, string name, MCPServerConfig config, IDebugLogger logger = null, bool skipStartup = false)
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
                // Create a clean copy of args to avoid duplicating flags
                var argsList = config.Args
                    .Where(arg => arg != "--stdio" && arg != "--http")
                    .ToList();
                
                logger?.Log($"Cleaned args: {string.Join(" ", argsList)}");
                
                // Check for communication mode in args
                bool usesStdio = config.Args.Contains("--stdio");
                bool usesHttp = config.Args.Contains("--http");
                string targetDir = null;
                
                // Get the target directory from the last argument if it's not a flag
                if (config.Args.Length > 0)
                {
                    var lastArg = config.Args[config.Args.Length - 1];
                    if (!lastArg.StartsWith("-") && !lastArg.Contains("@modelcontextprotocol"))
                    {
                        targetDir = lastArg;
                        logger?.Log($"Target directory: {targetDir}");
                    }
                }
                
                // Add the appropriate mode flag in the correct position
                if (usesStdio || !usesHttp) // Prefer stdio by default
                {
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
                    
                    // Insert --stdio at the appropriate position
                    if (insertIndex >= 0 && insertIndex <= argsList.Count)
                    {
                        argsList.Insert(insertIndex, "--stdio");
                        logger?.Log($"Adding --stdio flag at position {insertIndex}");
                    }
                    else
                    {
                        argsList.Add("--stdio");
                        logger?.Log("Adding --stdio flag at the end");
                    }
                }
                else if (usesHttp)
                {
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
                    if (insertIndex >= 0 && insertIndex <= argsList.Count)
                    {
                        argsList.Insert(insertIndex, "--http");
                        logger?.Log($"Adding --http flag at position {insertIndex}");
                    }
                    else
                    {
                        argsList.Add("--http");
                        logger?.Log("Adding --http flag at the end");
                    }
                }
                
                // Use a reliable directory instead of the target directory
                string workingDir = Path.GetTempPath();
                
                // Create the final args array
                var finalArgs = argsList.ToArray();
                logger?.Log($"Final args: {string.Join(" ", finalArgs)}");
                
                // Use cmd.exe to help find commands
                string command = "cmd.exe";
                string[] cmdArgs = new string[] { "/c", "npx" }.Concat(finalArgs).ToArray();
                logger?.Log($"Using command: {command} with args: {string.Join(" ", cmdArgs)}");
                
                // Create client
                var mcpClient = new StdioMCPServerClient(command, cmdArgs, workingDir, logger);
                
                // Register it first
                mcpClientFactory.RegisterMCPServer(serverName, mcpClient);
                logger?.Log($"Registered StdioMCPServerClient for '{serverName}'");
                
                // Only try to start it if not skipping startup
                if (!skipStartup)
                {
                    logger?.Log($"Trying to start server for '{serverName}'...");
                    var startTask = mcpClient.StartServerAsync();
                    startTask.GetAwaiter().GetResult();
                    
                    if (startTask.Result)
                    {
                        logger?.Log($"Successfully started server for '{serverName}'");
                    }
                    else
                    {
                        logger?.Log($"Failed to start server for '{serverName}', but it is still registered");
                    }
                }
                else
                {
                    logger?.Log($"Skipping startup for '{serverName}' (will be started on demand)");
                }
            }
            else if ((string.Equals(config.Command, "http", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(config.Command, "https", StringComparison.OrdinalIgnoreCase)) && 
                     config.Args != null && config.Args.Length > 0)
            {
                // Create an HTTP client
                string serverUrl = config.Args[0];
                if (!serverUrl.StartsWith("http://") && !serverUrl.StartsWith("https://"))
                {
                    serverUrl = $"http://{serverUrl}";
                }
                
                var httpClient = new SimplifiedMCPClient(serverUrl, logger);
                mcpClientFactory.RegisterMCPServer(serverName, httpClient);
                logger?.Log($"Registered SimplifiedMCPClient for '{serverName}' at URL {serverUrl}");
            }
            else
            {
                logger?.Log($"Unsupported server configuration for '{serverName}'");
            }
        }
    }
}
