using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using AIAgentTest.Commands;
using System.Collections.Generic;
using AIAgentTest.API_Clients.MCP;
using AIAgentTest.Services;
using AIAgentTest.Services.Interfaces;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AIAgentTest.Views;
using System.Diagnostics;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel for managing MCP servers - LoadServers method fixes
    /// </summary>
    public partial class MCPServerManagerViewModel : ViewModelBase
    {
        /// <summary>
        /// Loads the servers from the MCPClientFactory
        /// </summary>
        public void LoadServers(MCPClientFactory factory = null)
        {
            var mcpClientFactory = factory ?? _mcpClientFactory;
            Servers.Clear();
            
            try
            {
                var logger = ServiceProvider.GetService<IDebugLogger>();
                logger?.Log("MCPServerManagerViewModel: LoadServers called");
                
                // Get all registered server names
                var serverNames = mcpClientFactory.GetAllRegisteredServers();
                logger?.Log($"Found {serverNames.Count} registered server names: {string.Join(", ", serverNames)}");
                
                // Create a ViewModel for each server
                foreach (var serverName in serverNames)
                {
                    var serverClient = mcpClientFactory.GetMCPServer(serverName);
                    if (serverClient != null)
                    {
                        logger?.Log($"Creating ViewModel for server '{serverName}'");
                        
                        // Try to get command and args if this is a StdioMCPServerClient
                        string command = "npx";
                        string[] args = null;
                        
                        if (serverClient is StdioMCPServerClient stdioClient)
                        {
                            // This info is not directly accessible, so defaults are used
                            logger?.Log($"Server '{serverName}' is StdioMCPServerClient");
                        }
                        
                        var serverViewModel = new MCPServerViewModel
                        {
                            Name = serverName,
                            Command = command,
                            Args = args,
                            ServerClient = serverClient,
                            IsRunning = false, // Will be updated by RefreshServers
                            IsConnected = false, // Will be updated by RefreshServers
                            LastConnectionAttempt = DateTime.Now
                        };
                        
                        Servers.Add(serverViewModel);
                        logger?.Log($"Added server '{serverName}' to UI");
                    }
                    else
                    {
                        logger?.Log($"Skipping server '{serverName}' because client is null");
                    }
                }
                
                // Refresh the status of all servers
                logger?.Log("Refreshing server statuses");
                RefreshServers();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading servers: {ex.Message}");
                var logger = ServiceProvider.GetService<IDebugLogger>();
                logger?.Log($"Error loading servers: {ex.Message}");
                logger?.Log($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error loading servers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}