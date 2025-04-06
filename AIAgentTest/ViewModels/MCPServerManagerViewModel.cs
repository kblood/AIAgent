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
        /// Command to refresh all server statuses
        /// </summary>
        public ICommand RefreshServersCommand { get; }
        
        /// <summary>
        /// Command to start a server
        /// </summary>
        public ICommand StartServerCommand { get; }
        
        /// <summary>
        /// Command to stop a server
        /// </summary>
        public ICommand StopServerCommand { get; }
    
        /// <summary>
        /// Command to show debug information
        /// </summary>
        public ICommand ShowDebugCommand { get; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="mcpClientFactory">MCP client factory</param>
        public MCPServerManagerViewModel(MCPClientFactory mcpClientFactory)
        {
        _mcpClientFactory = mcpClientFactory ?? throw new ArgumentNullException(nameof(mcpClientFactory));
        Servers = new ObservableCollection<MCPServerViewModel>();
        
        // Load the existing servers
        LoadServers();
            
            // Commands
            AddServerCommand = new RelayCommand(() => AddServer());
            EditServerCommand = new RelayCommand(() => EditServer(), () => SelectedServer != null);
            RemoveServerCommand = new RelayCommand(() => RemoveServer(), () => SelectedServer != null);
            TestConnectionCommand = new RelayCommand(() => TestConnection(), () => SelectedServer != null);
            RefreshServersCommand = new RelayCommand(() => RefreshServers());
            StartServerCommand = new RelayCommand<MCPServerViewModel>((server) => StartServer(server), server => server != null && !server.IsRunning);
            StopServerCommand = new RelayCommand<MCPServerViewModel>((server) => StopServer(server), server => server != null && server.IsRunning);
            ShowDebugCommand = new RelayCommand(() => ShowDebug());
        }
        
        /// <summary>
        /// Loads the servers from the MCPClientFactory
        /// </summary>
        /// <summary>
        /// Loads the servers from the MCPClientFactory (private implementation)
        /// </summary>
        private void LoadServers()
        {
            LoadServers(_mcpClientFactory);
        }
        
        /// <summary>
        /// Loads the servers from the provided MCPClientFactory (public method)
        /// </summary>
        /// <param name="mcpClientFactory">The MCP client factory</param>
        public void LoadServers(MCPClientFactory mcpClientFactory)
        {
            Servers.Clear();
            
            try
            {
                // Get all registered server names
                var serverNames = _mcpClientFactory.GetAllRegisteredServers();
                
                // Create a ViewModel for each server
                foreach (var serverName in serverNames)
                {
                    var serverClient = _mcpClientFactory.GetMCPServer(serverName);
                    if (serverClient != null)
                    {
                        var serverViewModel = new MCPServerViewModel
                        {
                            Name = serverName,
                            ServerClient = serverClient,
                            IsRunning = false, // Will be updated by RefreshServers
                            IsConnected = false, // Will be updated by RefreshServers
                            LastConnectionAttempt = DateTime.Now
                        };
                        
                        Servers.Add(serverViewModel);
                    }
                    else
                    {
                        Debug.WriteLine($"Skipping server '{serverName}' because client is null");
                    }
                }
                
                // Refresh the status of all servers
                RefreshServers();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading servers: {ex.Message}");
                MessageBox.Show($"Error loading servers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Adds a new server
        /// </summary>
        private void AddServer()
        {
            try
            {
                // Create a new server editor ViewModel
                var serverEditorViewModel = new MCPServerEditorViewModel
                {
                    IsEditMode = false,
                    ServerName = "",
                    Command = "npx",
                    Arguments = "-y @modelcontextprotocol/server-filesystem C:/"
                };
                
                // Show the server editor dialog
                var dialog = new MCPServerDialog
                {
                    DataContext = serverEditorViewModel
                };
                
                var result = dialog.ShowDialog();
                if (result.HasValue && result.Value)
                {
                    // Register the new server
                    var serverName = serverEditorViewModel.ServerName;
                    var command = serverEditorViewModel.Command;
                    var args = serverEditorViewModel.Arguments.Replace("\\", "/").Split(' '); // Use forward slashes
                    
                    // Create a new server client - use StdioMCPServerClient for better reliability
                    var serverClient = new StdioMCPServerClient(command, args, "C:/", ServiceProvider.GetService<AIAgentTest.Services.Interfaces.IDebugLogger>());
                    
                    // Register with the client factory
                    _mcpClientFactory.RegisterMCPServer(serverName, serverClient);
                    
                    // Add to the UI
                    var serverViewModel = new MCPServerViewModel
                    {
                        Name = serverName,
                        ServerClient = serverClient,
                        IsRunning = false
                    };
                    
                    Servers.Add(serverViewModel);
                    
                    // Save the MCP server configuration
                    Debug.WriteLine($"Added new server: {serverName}");
                    
                    // Start the server automatically
                    StartServer(serverViewModel);
                    
                    // Save servers to configuration (this would typically be in a service)
                    SaveServersToConfiguration();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding server: {ex.Message}");
                MessageBox.Show($"Error adding server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Edits the selected server
        /// </summary>
        private void EditServer()
        {
            if (SelectedServer == null) return;
            
            try
            {
                // Create a server editor ViewModel with existing values
                var serverEditorViewModel = new MCPServerEditorViewModel
                {
                    IsEditMode = true,
                    ServerName = SelectedServer.Name,
                    Command = "npx", // You would need to extract these from the server client
                    Arguments = "-y @modelcontextprotocol/server-filesystem C:/"
                };
                
                // Show the server editor dialog
                var dialog = new MCPServerDialog
                {
                    DataContext = serverEditorViewModel
                };
                
                var result = dialog.ShowDialog();
                if (result.HasValue && result.Value)
                {
                    // Remove the old server if the name changed
                    if (SelectedServer.Name != serverEditorViewModel.ServerName)
                    {
                        RemoveServer(false);
                    }
                    
                    // Register the updated server
                    var serverName = serverEditorViewModel.ServerName;
                    var command = serverEditorViewModel.Command;
                    var args = serverEditorViewModel.Arguments.Replace("\\", "/").Split(' '); // Use forward slashes
                    
                    // Create a new server client - use StdioMCPServerClient for better reliability
                    var serverClient = new StdioMCPServerClient(command, args, "C:/", ServiceProvider.GetService<AIAgentTest.Services.Interfaces.IDebugLogger>());
                    
                    // Register with the client factory
                    _mcpClientFactory.RegisterMCPServer(serverName, serverClient);
                    
                    // Update or add to the UI
                    var existingServer = Servers.FirstOrDefault(s => s.Name == serverName);
                    if (existingServer != null)
                    {
                        existingServer.ServerClient = serverClient;
                    }
                    else
                    {
                        var serverViewModel = new MCPServerViewModel
                        {
                            Name = serverName,
                            ServerClient = serverClient,
                            IsRunning = false
                        };
                        
                        Servers.Add(serverViewModel);
                    }
                    
                    // Save the MCP server configuration
                    Debug.WriteLine($"Updated server: {serverName}");
                    
                    // Save servers to configuration
                    SaveServersToConfiguration();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error editing server: {ex.Message}");
                MessageBox.Show($"Error editing server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Removes the selected server
        /// </summary>
        private void RemoveServer(bool confirmDelete = true)
        {
            if (SelectedServer == null) return;
            
            try
            {
                if (confirmDelete)
                {
                    var result = MessageBox.Show($"Are you sure you want to remove the server '{SelectedServer.Name}'?",
                        "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    
                    if (result != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
                
                // Stop the server if it's running
                if (SelectedServer.IsRunning)
                {
                    StopServer(SelectedServer);
                }
                
                // Remove the server from the UI
                Servers.Remove(SelectedServer);
                
                // Save the MCP server configuration
                Debug.WriteLine($"Removed server: {SelectedServer.Name}");
                SelectedServer = null;
                
                // Save servers to configuration
                SaveServersToConfiguration();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error removing server: {ex.Message}");
                MessageBox.Show($"Error removing server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Tests the connection to the selected server
        /// </summary>
        private async void TestConnection()
        {
            if (SelectedServer == null) return;
            
            try
            {
                Debug.WriteLine($"Testing connection to server: {SelectedServer.Name}");
                SelectedServer.IsConnecting = true;
                
                if (SelectedServer.ServerClient == null)
                {
                    Debug.WriteLine($"ServerClient is null for server {SelectedServer.Name}");
                    MessageBox.Show($"Server client is not available for {SelectedServer.Name}. Try adding the server again.", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                var isAvailable = await SelectedServer.ServerClient.IsAvailableAsync();
                
                SelectedServer.IsRunning = isAvailable;
                SelectedServer.IsConnected = isAvailable; // Update IsConnected to match IsRunning
                SelectedServer.IsConnecting = false;
                
                if (isAvailable)
                {
                    Debug.WriteLine($"Successfully connected to server: {SelectedServer.Name}");
                    MessageBox.Show($"Successfully connected to server: {SelectedServer.Name}", 
                        "Connection Test", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    Debug.WriteLine($"Failed to connect to server: {SelectedServer.Name}");
                    var startResult = MessageBox.Show($"Failed to connect to server: {SelectedServer.Name}. Would you like to start it?", 
                        "Connection Test", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    
                    if (startResult == MessageBoxResult.Yes)
                    {
                        StartServer(SelectedServer);
                    }
                }
            }
            catch (Exception ex)
            {
                SelectedServer.IsConnecting = false;
                Debug.WriteLine($"Error testing connection: {ex.Message}");
                MessageBox.Show($"Error testing connection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Refreshes the status of all servers
        /// </summary>
        private async void RefreshServers()
        {
            try
            {
                foreach (var server in Servers)
                {
                    server.IsConnecting = true;
                    
                    try
                    {
                        if (server.ServerClient == null) 
                        {
                            Debug.WriteLine($"ServerClient is null for server {server.Name}");
                            server.IsRunning = false;
                            server.IsConnected = false;
                            server.AvailableToolCount = 0;
                            continue;
                        }
                        
                        var isAvailable = await server.ServerClient.IsAvailableAsync();
                        server.IsRunning = isAvailable;
                        server.IsConnected = isAvailable; // Update IsConnected to match
                        server.LastConnectionAttempt = DateTime.Now;
                        
                        // If available, get tool count
                        if (isAvailable) {
                            try {
                                var tools = await server.ServerClient.GetToolsAsync();
                                server.AvailableToolCount = tools.Count;
                            } catch (Exception toolEx) {
                                Debug.WriteLine($"Error getting tools: {toolEx.Message}");
                                server.AvailableToolCount = 0;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error checking server status: {ex.Message}");
                        server.IsRunning = false;
                    }
                    
                    server.IsConnecting = false;
                }
                
                Debug.WriteLine("Refreshed server statuses");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing servers: {ex.Message}");
                MessageBox.Show($"Error refreshing servers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Starts the specified server
        /// </summary>
        private async void StartServer(MCPServerViewModel server)
        {
            if (server == null) return;
            
            try
            {
                Debug.WriteLine($"Starting server: {server.Name}");
                server.IsConnecting = true;
                
                if (server.ServerClient == null)
                {
                    Debug.WriteLine($"ServerClient is null for server {server.Name}");
                    MessageBox.Show($"Server client is not available for {server.Name}. Try adding the server again.", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    server.IsConnecting = false;
                    return;
                }
                
                var success = await server.ServerClient.StartServerAsync();
                
                server.IsRunning = success;
                server.IsConnected = success; // Update IsConnected to match
                server.IsActive = success; // Set IsActive as well
                server.IsConnecting = false;
                
                if (success)
                {
                    Debug.WriteLine($"Successfully started server: {server.Name}");
                    MessageBox.Show($"Successfully started server: {server.Name}", 
                        "Server Started", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    Debug.WriteLine($"Failed to start server: {server.Name}");
                    MessageBox.Show($"Failed to start server: {server.Name}", 
                        "Server Start Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                server.IsConnecting = false;
                Debug.WriteLine($"Error starting server: {ex.Message}");
                MessageBox.Show($"Error starting server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Stops the specified server
        /// </summary>
        private void StopServer(MCPServerViewModel server)
        {
            if (server == null) return;
            
            try
            {
                Debug.WriteLine($"Stopping server: {server.Name}");
                
                if (server.ServerClient == null)
                {
                    Debug.WriteLine($"ServerClient is null for server {server.Name}");
                    MessageBox.Show($"Server client is not available for {server.Name}. Try adding the server again.", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                server.ServerClient.StopServer();
                server.IsRunning = false;
                server.IsConnected = false;
                server.IsActive = false;
                
                Debug.WriteLine($"Stopped server: {server.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping server: {ex.Message}");
                MessageBox.Show($"Error stopping server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Shows debug information for the selected server
        /// </summary>
        private async void ShowDebug()
        {
            if (SelectedServer == null) return;
            
            try
            {
                // Create a simple text window with debug info
                var debugInfo = $"Server Name: {SelectedServer.Name}\n" +
                                $"Is Running: {SelectedServer.IsRunning}\n" +
                                $"Is Connected: {SelectedServer.IsConnected}\n" +
                                $"Is Active: {SelectedServer.IsActive}\n";
                                
                if (SelectedServer.ServerClient != null)
                {
                    debugInfo += $"Client Type: {SelectedServer.ServerClient.GetType().Name}\n";
                    
                    // Try to get available tools
                    try 
                    {
                        var tools = await SelectedServer.ServerClient.GetToolsAsync();
                        SelectedServer.AvailableToolCount = tools.Count;
                        
                        if (tools.Count > 0) 
                        {
                            debugInfo += "\nAvailable Tools:\n";
                            foreach (var tool in tools) 
                            {
                                debugInfo += $"- {tool.Name}: {tool.Description}\n";
                            }
                        } 
                        else 
                        {
                            debugInfo += "\nNo tools available.\n";
                        }
                    } 
                    catch (Exception toolEx) 
                    {
                        debugInfo += $"\nError getting tools: {toolEx.Message}\n";
                    }
                }
                else
                {
                    debugInfo += $"Client: null (server client not available)\n";
                }
                
                MessageBox.Show(debugInfo, "Server Debug Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing debug info: {ex.Message}");
                MessageBox.Show($"Error showing debug info: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Saves the servers to configuration
        /// </summary>
        private void SaveServersToConfiguration()
        {
            try
            {
                Debug.WriteLine("Saving server configuration");
                
                // Create a MCPServersConfig object
                var config = new Services.MCP.MCPServerRegistration.MCPServersConfig
                {
                    McpServers = new Dictionary<string, Services.MCP.MCPServerRegistration.MCPServerConfig>()
                };
                
                // Add each server to the configuration
                foreach (var server in Servers)
                {
                    if (server.Name == "FileServer")
                    {
                        // Special case for FileServer - always use stdio
                        // Process args properly to avoid duplicate --stdio
                        List<string> args = new List<string>();
                        args.Add("-y");
                        args.Add("@modelcontextprotocol/server-filesystem");
                        args.Add("--stdio"); // Add stdio flag in the right position
                        args.Add("C:/");
                        
                        config.McpServers[server.Name] = new Services.MCP.MCPServerRegistration.MCPServerConfig
                        {
                            Command = "npx",
                            Args = args.ToArray()
                        };
                    }
                    else if (server.ServerClient != null)
                    {
                        // Process args properly for regular servers
                        List<string> args = new List<string>();
                        
                        // Try to get args from server if available
                        if (server.Args != null && server.Args.Length > 0)
                        {
                            // Create a clean copy without --stdio
                            args = server.Args.Where(a => a != "--stdio").ToList();
                        }
                        else
                        {
                            // Default args
                            args.Add("-y");
                            args.Add("@modelcontextprotocol/server-filesystem");
                            args.Add("C:/");
                        }
                        
                        // Find where to insert --stdio
                        int insertIndex = -1;
                        for (int i = 0; i < args.Count; i++)
                        {
                            if (args[i].Contains("@modelcontextprotocol"))
                            {
                                insertIndex = i + 1;
                                break;
                            }
                        }
                        
                        if (insertIndex >= 0)
                        {
                            // Insert --stdio after the package name
                            args.Insert(insertIndex, "--stdio");
                        }
                        else 
                        {
                            // If no package name found, add after -y if it exists
                            int yIndex = args.IndexOf("-y");
                            if (yIndex >= 0)
                            {
                                args.Insert(yIndex + 1, "--stdio"); 
                            }
                            else
                            {
                                // Add at the beginning as last resort
                                args.Insert(0, "--stdio");
                            }
                        }
                        
                        config.McpServers[server.Name] = new Services.MCP.MCPServerRegistration.MCPServerConfig
                        {
                            Command = "npx",
                            Args = args.ToArray()
                        };
                    }
                }
                
                // Serialize to JSON
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                };
                
                var json = System.Text.Json.JsonSerializer.Serialize(config, options);
                Debug.WriteLine($"Generated server config JSON: {json}");
                
                // Save to settings
                Properties.Settings.Default.MCPServers = json;
                Properties.Settings.Default.Save();
                
                // Also save to .roo folder
                var rootDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var rooPath = System.IO.Path.Combine(rootDir, ".roo");
                
                // Create directory if it doesn't exist
                if (!System.IO.Directory.Exists(rooPath))
                {
                    System.IO.Directory.CreateDirectory(rooPath);
                }
                
                // Save to mcp.json file
                var filePath = System.IO.Path.Combine(rooPath, "mcp.json");
                System.IO.File.WriteAllText(filePath, json);
                
                // Also try to save to the root directory's .roo folder
                try
                {
                    var solutionRootDir = System.IO.Path.Combine(rootDir, "..", "..", "..");
                    var solutionRooPath = System.IO.Path.Combine(solutionRootDir, ".roo");
                    
                    // Create directory if it doesn't exist
                    if (!System.IO.Directory.Exists(solutionRooPath))
                    {
                        System.IO.Directory.CreateDirectory(solutionRooPath);
                    }
                    
                    // Save to mcp.json file
                    var solutionFilePath = System.IO.Path.Combine(solutionRooPath, "mcp.json");
                    System.IO.File.WriteAllText(solutionFilePath, json);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error saving to solution root .roo folder: {ex.Message}");
                }
                
                Debug.WriteLine("Server configuration saved successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving server configuration: {ex.Message}");
            }
        }
    }
}