using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using AIAgentTest.Commands;
using System.Collections.Generic;
using AIAgentTest.API_Clients.MCP;
using System.Linq;
using System.Threading.Tasks;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel for managing MCP servers
    /// </summary>
    public class MCPServerManagerViewModel : ViewModelBase
    {
        private readonly MCPClientFactory _mcpClientFactory;
        private ObservableCollection<MCPServerViewModel> _servers;
        private MCPServerViewModel _selectedServer;
        
        /// <summary>
        /// Collection of MCP servers
        /// </summary>
        public ObservableCollection<MCPServerViewModel> Servers
        {
            get => _servers;
            set => SetProperty(ref _servers, value);
        }
        
        /// <summary>
        /// Currently selected server
        /// </summary>
        public MCPServerViewModel SelectedServer
        {
            get => _selectedServer;
            set => SetProperty(ref _selectedServer, value);
        }
        
        /// <summary>
        /// Command to add a new server
        /// </summary>
        public ICommand AddServerCommand { get; }
        
        /// <summary>
        /// Command to edit the selected server
        /// </summary>
        public ICommand EditServerCommand { get; }
        
        /// <summary>
        /// Command to remove the selected server
        /// </summary>
        public ICommand RemoveServerCommand { get; }
        
        /// <summary>
        /// Command to test the connection to the selected server
        /// </summary>
        public ICommand TestConnectionCommand { get; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mcpClientFactory">MCP client factory</param>
        public MCPServerManagerViewModel(MCPClientFactory mcpClientFactory)
        {
            _mcpClientFactory = mcpClientFactory;
            Servers = new ObservableCollection<MCPServerViewModel>();
            
            // Load existing servers from settings
            LoadServers();
            
            // Commands
            AddServerCommand = new RelayCommand(ExecuteAddServer);
            EditServerCommand = new RelayCommand(ExecuteEditServer, CanEditServer);
            RemoveServerCommand = new RelayCommand(ExecuteRemoveServer, CanRemoveServer);
            TestConnectionCommand = new RelayCommand(ExecuteTestConnection, CanTestConnection);
        }
        
        /// <summary>
        /// Load servers from settings
        /// </summary>
        private void LoadServers()
        {
            // Load from application settings
            var serversString = Properties.Settings.Default.MCPServers;
            if (string.IsNullOrEmpty(serversString))
            {
                return; // No servers configured
            }
            
            // Get enabled/disabled server settings
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
            
            // Try parsing as JSON (ModelContextProtocol standard)
            try
            {
                var serversConfig = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(serversString);
                
                if (serversConfig != null && serversConfig.ContainsKey("mcpServers"))
                {
                    var mcpServers = serversConfig["mcpServers"] as Dictionary<string, object>;
                    
                    foreach (var serverEntry in mcpServers)
                    {
                        var name = serverEntry.Key;
                        var configObj = serverEntry.Value as Dictionary<string, object>;
                        
                        if (configObj != null && configObj.TryGetValue("command", out var cmdObj) && 
                            configObj.TryGetValue("args", out var argsObj))
                        {
                            string command = cmdObj.ToString();
                            var argsArray = System.Text.Json.JsonSerializer.Deserialize<string[]>(argsObj.ToString());
                            
                            var server = new MCPServerViewModel
                            {
                                Name = name,
                                Command = command,
                                Args = argsArray,
                                IsEnabled = !disabledServers.Contains(name) && 
                                           (enabledServers.Count == 0 || enabledServers.Contains(name))
                            };
                            
                            Servers.Add(server);
                            
                            // Register enabled servers with the factory
                            if (server.IsEnabled)
                            {
                                RegisterServer(server);
                            }
                        }
                    }
                    
                    // JSON loaded successfully, return
                    return;
                }
            }
            catch
            {
                // Not a valid JSON or error, try legacy format
            }
            
            // Legacy format
            var serverList = serversString.Split(';');
            
            foreach (var serverString in serverList)
            {
                if (string.IsNullOrWhiteSpace(serverString)) continue;
                
                var parts = serverString.Split('|');
                if (parts.Length < 3) continue;
                
                var name = parts[0];
                var url = parts[1];
                var type = parts[2];
                var isEnabledStr = parts.Length > 3 && bool.TryParse(parts[3], out bool enabled) && enabled;
                
                // Check specific enable/disable settings
                var isEnabled = !disabledServers.Contains(name) && 
                               (enabledServers.Count == 0 || enabledServers.Contains(name) || isEnabledStr);
                
                // For legacy format, create appropriate Args based on type
                string command = "npx";
                string[] args = null;
                
                if (type.ToLowerInvariant() == "filesystem")
                {
                    args = new[] { "-y", "@modelcontextprotocol/server-filesystem", url };
                }
                
                if (args != null)
                {
                    var server = new MCPServerViewModel
                    {
                        Name = name,
                        Command = command,
                        Args = args,
                        IsEnabled = isEnabled
                    };
                    
                    Servers.Add(server);
                    
                    // Register enabled servers with the factory
                    if (server.IsEnabled)
                    {
                        RegisterServer(server);
                    }
                }
            }
        }
        
        /// <summary>
        /// Save servers to settings
        /// </summary>
        private void SaveServers()
        {
            // Save to application settings - using MCP standard format
            var mcpConfig = new Dictionary<string, Dictionary<string, object>>();
            var serverConfigs = new Dictionary<string, object>();
            
            foreach (var server in Servers)
            {
                serverConfigs[server.Name] = new Dictionary<string, object>
                {
                    { "command", server.Command },
                    { "args", server.Args }
                };
            }
            
            mcpConfig["mcpServers"] = serverConfigs;
            
            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            Properties.Settings.Default.MCPServers = System.Text.Json.JsonSerializer.Serialize(mcpConfig, options);
            
            // Save enabled/disabled server state
            var enabledServers = new List<string>();
            var disabledServers = new List<string>();
            
            foreach (var server in Servers)
            {
                if (server.IsEnabled)
                    enabledServers.Add(server.Name);
                else
                    disabledServers.Add("!" + server.Name);
            }
            
            Properties.Settings.Default.EnabledMCPServers = string.Join(";", enabledServers.Concat(disabledServers));
            Properties.Settings.Default.Save();
        }
        
        /// <summary>
        /// Register a server with the MCP client factory
        /// </summary>
        private void RegisterServer(MCPServerViewModel server)
        {
            if (!server.IsEnabled) return;
            
            try
            {
                // Create the appropriate server client based on server type
                switch (server.Type.ToLowerInvariant())
                {
                    case "filesystem":
                        var fileSystemServer = new FileSystemMCPServerClient(server.Command, server.Args);
                        _mcpClientFactory.RegisterMCPServer(server.Name, fileSystemServer);
                        break;
                    case "custom":
                        // TODO: Support other server types as needed
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error registering server {server.Name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Unregister a server from the MCP client factory
        /// </summary>
        private void UnregisterServer(MCPServerViewModel server)
        {
            // This would require extending MCPClientFactory to support unregistering
            // For now, we'll leave this as a placeholder
        }
        
        /// <summary>
        /// Add a new server
        /// </summary>
        private void ExecuteAddServer()
        {
            var dialog = new Views.MCPServerDialog();
            var viewModel = new MCPServerEditorViewModel(null);
            dialog.DataContext = viewModel;
            
            if (dialog.ShowDialog() == true)
            {
                // Create new server from the editor
                var newServer = new MCPServerViewModel
                {
                    Name = viewModel.Name,
                    Command = viewModel.Command,
                    Args = viewModel.Args,
                    IsEnabled = viewModel.IsEnabled
                };
                
                Servers.Add(newServer);
                
                // Register if enabled
                if (newServer.IsEnabled)
                {
                    RegisterServer(newServer);
                }
                
                // Save changes
                SaveServers();
            }
        }
        
        /// <summary>
        /// Edit the selected server
        /// </summary>
        private void ExecuteEditServer()
        {
            if (SelectedServer == null) return;
            
            var dialog = new Views.MCPServerDialog();
            var viewModel = new MCPServerEditorViewModel(SelectedServer);
            dialog.DataContext = viewModel;
            
            if (dialog.ShowDialog() == true)
            {
                // Get the original enabled state
                bool wasEnabled = SelectedServer.IsEnabled;
                
                // Update server from the editor
                SelectedServer.Name = viewModel.Name;
                SelectedServer.Command = viewModel.Command;
                SelectedServer.Args = viewModel.Args;
                SelectedServer.IsEnabled = viewModel.IsEnabled;
                
                // Handle registration changes
                if (!wasEnabled && SelectedServer.IsEnabled)
                {
                    RegisterServer(SelectedServer);
                }
                else if (wasEnabled && !SelectedServer.IsEnabled)
                {
                    UnregisterServer(SelectedServer);
                }
                else if (wasEnabled && SelectedServer.IsEnabled)
                {
                    // Server is still enabled but might have changed properties
                    UnregisterServer(SelectedServer);
                    RegisterServer(SelectedServer);
                }
                
                // Save changes
                SaveServers();
            }
        }
        
        /// <summary>
        /// Can edit the selected server
        /// </summary>
        private bool CanEditServer() => SelectedServer != null;
        
        /// <summary>
        /// Remove the selected server
        /// </summary>
        private void ExecuteRemoveServer()
        {
            if (SelectedServer == null) return;
            
            // Unregister if enabled
            if (SelectedServer.IsEnabled)
            {
                UnregisterServer(SelectedServer);
            }
            
            // Remove from collection
            Servers.Remove(SelectedServer);
            
            // Save changes
            SaveServers();
        }
        
        /// <summary>
        /// Can remove the selected server
        /// </summary>
        private bool CanRemoveServer() => SelectedServer != null;
        
        /// <summary>
        /// Test the connection to the selected server
        /// </summary>
        private async void ExecuteTestConnection()
        {
            if (SelectedServer == null) return;
            
            SelectedServer.IsConnecting = true;
            
            try
            {
                // Create a temporary client to test the connection
                IMCPServerClient client = null;
                
                switch (SelectedServer.Type.ToLowerInvariant())
                {
                    case "filesystem":
                        client = new FileSystemMCPServerClient(SelectedServer.Command, SelectedServer.Args);
                        break;
                }
                
                if (client != null)
                {
                    // Test the connection
                    bool isAvailable = await client.IsAvailableAsync();
                    SelectedServer.IsConnected = isAvailable;
                    
                    if (isAvailable)
                    {
                        // Get available tools
                        var tools = await client.GetToolsAsync();
                        SelectedServer.AvailableToolCount = tools.Count;
                    }
                    else
                    {
                        SelectedServer.ConnectionError = "Failed to connect to server";
                    }
                }
                else
                {
                    SelectedServer.IsConnected = false;
                    SelectedServer.ConnectionError = "Unsupported server type";
                }
            }
            catch (Exception ex)
            {
                SelectedServer.IsConnected = false;
                SelectedServer.ConnectionError = ex.Message;
            }
            finally
            {
                SelectedServer.IsConnecting = false;
                SelectedServer.LastConnectionAttempt = DateTime.Now;
            }
        }
        
        /// <summary>
        /// Can test the connection to the selected server
        /// </summary>
        private bool CanTestConnection() => SelectedServer != null;
    }
}