using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AIAgentTest.API_Clients.MCP;
using AIAgentTest.Services.Interfaces;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Helper class for registering external MCP servers
    /// </summary>
    public static class MCPServerRegistration
    {
        /// <summary>
        /// Registers MCP servers with the factory
        /// </summary>
        /// <param name="mcpClientFactory">MCP client factory</param>
        public static async Task RegisterMCPServersAsync(MCPClientFactory mcpClientFactory)
        {
            // Register the filesystem MCP server if available
            await TryRegisterFileSystemServerAsync(mcpClientFactory);
            
            // Add other server registrations as needed
        }
        
        /// <summary>
        /// Attempts to register the filesystem MCP server
        /// </summary>
        private static async Task TryRegisterFileSystemServerAsync(MCPClientFactory mcpClientFactory)
        {
            try
            {
                // Get the server URL from settings (or use a default)
                var serverUrl = GetSettingOrDefault("FileSystemMCPServerUrl", "http://localhost:3000");
                
                // Create the server client
                var fileSystemServer = new FileSystemMCPServerClient(serverUrl);
                
                // Check if the server is available
                if (await fileSystemServer.IsAvailableAsync())
                {
                    // Register with the factory
                    mcpClientFactory.RegisterMCPServer("filesystem", fileSystemServer);
                    
                    Console.WriteLine("FileSystem MCP Server registered successfully");
                    
                    // Get available tools from the server
                    var tools = await fileSystemServer.GetAvailableToolsAsync();
                    
                    // Get the tool registry
                    var toolRegistry = ServiceProvider.GetService<IToolRegistry>();
                    
                    // Register tools with the registry
                    foreach (var tool in tools)
                    {
                        toolRegistry.RegisterTool(tool, async (parameters) => {
                            // Execute the tool on the server
                            return await fileSystemServer.ExecuteToolAsync(tool.Name, parameters);
                        });
                        
                        Console.WriteLine($"Registered external tool: {tool.Name}");
                    }
                }
                else
                {
                    Console.WriteLine("FileSystem MCP Server is not available");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error registering FileSystem MCP Server: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets a setting or returns a default value
        /// </summary>
        private static string GetSettingOrDefault(string key, string defaultValue)
        {
            // In a real implementation, this would get the setting from a configuration source
            return defaultValue;
        }
    }
}
