using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AIAgentTest.API_Clients.MCP;
using System.Linq;

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
            public string Command { get; set; }
            public string[] Args { get; set; }
            [JsonIgnore]
            public bool IsEnabled { get; set; } = true;
        }
        
        /// <summary>
        /// MCP Servers configuration class
        /// </summary>
        public class MCPServersConfig
        {
            public Dictionary<string, MCPServerConfig> McpServers { get; set; } = new Dictionary<string, MCPServerConfig>();
        }

        /// <summary>
        /// Register MCP servers from settings
        /// </summary>
        /// <param name="mcpClientFactory">MCP client factory</param>
        /// <returns>Task</returns>
        public static async Task RegisterMCPServersAsync(MCPClientFactory mcpClientFactory)
        {
            if (mcpClientFactory == null)
                throw new ArgumentNullException(nameof(mcpClientFactory));
            
            var serversString = Properties.Settings.Default.MCPServers;
            if (string.IsNullOrEmpty(serversString))
                return;
            
            // Try to parse the server config as JSON first (ModelContextProtocol standard)
            try
            {
                var serversConfig = JsonSerializer.Deserialize<MCPServersConfig>(serversString);
                if (serversConfig?.McpServers != null)
                {
                    // Get enabled servers from settings
                    var enabledServers = new HashSet<string>();
                    var disabledServers = new HashSet<string>();
                    
                    var enabledServersStr = Properties.Settings.Default.EnabledMCPServers;
                    if (!string.IsNullOrEmpty(enabledServersStr))
                    {
                        var serverEntries = enabledServersStr.Split(';', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var server in serverEntries)
                        {
                            if (server.StartsWith("!"))
                                disabledServers.Add(server.Substring(1));
                            else
                                enabledServers.Add(server);
                        }
                    }
                    
                    foreach (var serverEntry in serversConfig.McpServers)
                    {
                        try
                        {
                            var name = serverEntry.Key;
                            var config = serverEntry.Value;
                            
                            // Check if explicitly enabled/disabled
                            if (enabledServers.Count > 0 || disabledServers.Count > 0)
                            {
                                config.IsEnabled = !disabledServers.Contains(name) && 
                                              (enabledServers.Count == 0 || enabledServers.Contains(name));
                            }
                            
                            if (!config.IsEnabled)
                                continue;
                                
                            RegisterServerFromConfig(mcpClientFactory, name, config);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error registering MCP server {serverEntry.Key}: {ex.Message}");
                        }
                    }
                    
                    // Successfully parsed JSON config, no need to try the legacy format
                    return;
                }
            }
            catch 
            {
                // Not valid JSON, try legacy format
            }
            
            // Legacy format: name|url|type|enabled
            var serversList = serversString.Split(';');
            
            foreach (var serverString in serversList.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                try
                {
                    var parts = serverString.Split('|');
                    if (parts.Length < 3)
                        continue;
                    
                    var name = parts[0];
                    var url = parts[1];
                    var type = parts[2];
                    var isEnabled = parts.Length > 3 && bool.TryParse(parts[3], out bool enabled) && enabled;
                    
                    if (!isEnabled)
                        continue;
                    
                    // Register the server based on type
                    switch (type.ToLowerInvariant())
                    {
                        case "filesystem":
                            // Use default npx command for filesystem
                            var config = new MCPServerConfig
                            {
                                Command = "npx",
                                Args = new[] { "-y", "@modelcontextprotocol/server-filesystem", url },
                                IsEnabled = true
                            };
                            RegisterServerFromConfig(mcpClientFactory, name, config);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error registering MCP server: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Register a server from its configuration
        /// </summary>
        /// <param name="mcpClientFactory">MCP client factory</param>
        /// <param name="name">Server name</param>
        /// <param name="config">Server configuration</param>
        private static void RegisterServerFromConfig(MCPClientFactory mcpClientFactory, string name, MCPServerConfig config)
        {
            if (string.Equals(config.Command, "npx", StringComparison.OrdinalIgnoreCase) &&
                config.Args.Length > 0 && 
                config.Args[0].Contains("@modelcontextprotocol/server-filesystem"))
            {
                var fileSystemServer = new FileSystemMCPServerClient(config.Command, config.Args);
                mcpClientFactory.RegisterMCPServer(name, fileSystemServer);
            }
            // Add other server types as needed
        }
    }
}
