using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIAgentTest.API_Clients.MCP;
using AIAgentTest.Services.Interfaces;
using System.Text.Json;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Helper for registering MCP server tools
    /// </summary>
    public static class MCPServerToolRegistration
    {
        /// <summary>
        /// Register tools from MCP servers
        /// </summary>
        public static async Task RegisterServerToolsAsync(IToolRegistry toolRegistry, MCPClientFactory mcpClientFactory, IDebugLogger logger = null)
        {
            if (toolRegistry == null || mcpClientFactory == null)
                return;
                
            logger?.Log("Registering MCP server tools...");
            
            try
            {
                // Get all registered servers
                var serverNames = mcpClientFactory.GetAllRegisteredServers();
                
                foreach (var serverName in serverNames)
                {
                    try
                    {
                        var serverClient = mcpClientFactory.GetMCPServer(serverName);
                        if (serverClient == null)
                        {
                            logger?.Log($"Server '{serverName}' not found in the factory");
                            continue;
                        }
                        
                        logger?.Log($"Getting tools from server '{serverName}'...");
                        
                        // Start the server if not already running
                        var isAvailable = await serverClient.IsAvailableAsync();
                        if (!isAvailable)
                        {
                            logger?.Log($"Starting server '{serverName}'...");
                            isAvailable = await serverClient.StartServerAsync();
                            
                            if (!isAvailable)
                            {
                                logger?.Log($"Failed to start server '{serverName}'");
                                logger?.Log($"Continuing with fallback tool implementations for '{serverName}'");
                                // Continue anyway - will use fallback implementations
                            }
                        }
                        
                        // Get tools from the server
                        var tools = await serverClient.GetToolsAsync();
                        
                        logger?.Log($"Server '{serverName}' returned {tools.Count} tools");
                        
                        // Register each tool
                        foreach (var tool in tools)
                        {
                            // Make sure the tool has server metadata
                            if (tool.Metadata == null)
                            {
                                tool.Metadata = new Dictionary<string, object>();
                            }
                            
                            // Add server name to metadata if not already there
                            if (!tool.Metadata.ContainsKey("server_name"))
                            {
                                tool.Metadata["server_name"] = serverName;
                            }
                            
                            // Register the tool with a handler that delegates to the server
                            toolRegistry.RegisterTool(tool, async (input) => 
                            {
                                logger?.Log($"Executing tool '{tool.Name}' on server '{serverName}'");
                                return await serverClient.ExecuteToolAsync(tool.Name, input);
                            });
                            
                            logger?.Log($"Registered tool '{tool.Name}' from server '{serverName}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.Log($"Error getting tools from server '{serverName}': {ex.Message}");
                    }
                }
                
                logger?.Log("MCP server tool registration completed");
            }
            catch (Exception ex)
            {
                logger?.Log($"Error registering MCP server tools: {ex.Message}");
            }
        }
    }
}
