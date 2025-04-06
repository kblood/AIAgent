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
        // The RegisterServerFromConfig method updated to fix --stdio issues
        private static void RegisterServerFromConfig(MCPClientFactory mcpClientFactory, string name, MCPServerConfig config, IDebugLogger logger = null)
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
                
                // Create the final args array
                var finalArgs = argsList.ToArray();
                logger?.Log($"Final args: {string.Join(" ", finalArgs)}");
                
                // Use reliable temp path instead of C:/ directly
                string workingDir = Path.GetTempPath();
                if (Directory.Exists(targetDir))
                {
                    workingDir = targetDir;
                }
                
                logger?.Log($"Using working directory: {workingDir}");
                
                // Determine if we should use the StdioMCPServerClient based on the command arguments
                if (usesStdio || !usesHttp) // Default to stdio if not specified
                {
                    // Use StdioMCPServerClient for stdio mode
                    logger?.Log($"Creating StdioMCPServerClient for '{serverName}' with command {config.Command}");
                    var mcpClient = new StdioMCPServerClient(config.Command, finalArgs, workingDir, logger);
                    
                    // Start the server
                    var startTask = mcpClient.StartServerAsync();
                    startTask.GetAwaiter().GetResult();
                    
                    if (!startTask.Result)
                    {
                        logger?.Log($"Failed to start StdioMCPServerClient for '{serverName}'");
                        mcpClient.Dispose();
                        
                        // Fallback to HTTP if stdio fails
                        logger?.Log("Falling back to HTTP mode...");
                        
                        // Default URL if not specified
                        string serverUrl = "http://localhost:3000";
                        
                        // Create a new args array with the --http flag instead of --stdio
                        var httpArgsList = argsList
                            .Where(arg => arg != "--stdio")
                            .ToList();
                        
                        // Find the best place to insert the flag - right after the package name
                        int insertIndex = 0;
                        for (int i = 0; i < httpArgsList.Count; i++)
                        {
                            if (httpArgsList[i].Contains("@modelcontextprotocol"))
                            {
                                insertIndex = i + 1;
                                break;
                            }
                        }
                        
                        // Insert --http at the appropriate position
                        if (insertIndex >= 0 && insertIndex <= httpArgsList.Count)
                        {
                            httpArgsList.Insert(insertIndex, "--http");
                        }
                        else
                        {
                            httpArgsList.Add("--http");
                        }
                        
                        var fallbackClient = new SimplifiedMCPClient(serverUrl, config.Command, httpArgsList.ToArray(), logger);
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
                    logger?.Log($"Creating process-managing SimplifiedMCPClient for '{serverName}' with command {config.Command}");
                    
                    // Default URL if not specified
                    string serverUrl = "http://localhost:3000";
                    
                    var mcpClient = new SimplifiedMCPClient(serverUrl, config.Command, finalArgs, logger);
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
                var mcpClient = new StdioMCPServerClient(command, args, Path.GetTempPath(), logger);
                
                // Start the server
                var startTask = mcpClient.StartServerAsync();
                startTask.GetAwaiter().GetResult();
                
                if (!startTask.Result)
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