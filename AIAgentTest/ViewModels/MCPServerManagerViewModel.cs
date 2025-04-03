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
            // MCPServers setting isn't defined yet, using empty array for now
            var serverList = Array.Empty<string>();
            // In a production implementation, this would load from Properties.Settings.Default.MCPServers
            
            foreach (var serverString in serverList)
            {
                if (string.IsNullOrWhiteSpace(serverString)) continue;
                
                var parts = serverString.Split('|');
                if (parts.Length < 3) continue;
                
                var server = new MCPServerViewModel
                {
                    Name = parts[0],
                    Url = parts[1],
                    Type = parts[2],
                    IsEnabled = parts.Length > 3 && bool.TryParse(parts[3], out bool enabled) && enabled
                };
                
                Servers.Add(server);
                
                // Register enabled servers with the factory
                if (server.IsEnabled)
                {
                    RegisterServer(server);
                }
            }
        }
        
        /// <summary>
        /// Save servers to settings
        /// </summary>
        private void SaveServers()
        {
            // Save to application settings
            var serverStrings = new List<string>();
            
            foreach (var server in Servers)
            {
                serverStrings.Add($"{server.Name}|{server.Url}|{server.Type}|{server.IsEnabled}");
            }
            
            // Properties.Settings.Default.MCPServers = string.Join(";", serverStrings);
            // Properties.Settings.Default.Save();
            // In a production implementation, this would save to application settings
        }
        
        /// <summary>
        /// Register a server with the MCP client factory
        /// </summary>
        private void RegisterServer(MCPServerViewModel server)
        {
            if (!server.IsEnabled) return;
            
            // Create the appropriate server client based on server type
            // This will need to be adapted based on the actual server client implementations
            switch (server.Type.ToLowerInvariant())
            {
                case "filesystem":
                    // Example: _mcpClientFactory.RegisterMCPServer(server.Name, new FilesystemMCPServerClient(server.Url));
                    break;
                case "database":
                    // Example: _mcpClientFactory.RegisterMCPServer(server.Name, new DatabaseMCPServerClient(server.Url));
                    break;
                case "custom":
                    // Example: _mcpClientFactory.RegisterMCPServer(server.Name, new CustomMCPServerClient(server.Url));
                    break;
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
            // var dialog = new Views.MCPServerDialog(); // Uncomment when the dialog view is implemented
            bool dialogResult = false; // Simulating dialog interaction
            var viewModel = new MCPServerEditorViewModel(null);
            // dialog.DataContext = viewModel; // Uncomment when the dialog view is implemented
            
            if (dialogResult) // Simulate dialog result for now
            {
                // Create new server from the editor
                var newServer = new MCPServerViewModel
                {
                    Name = viewModel.Name,
                    Url = viewModel.Url,
                    Type = viewModel.Type,
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
            
            // var dialog = new Views.MCPServerDialog(); // Uncomment when the dialog view is implemented
            bool dialogResult = false; // Simulating dialog interaction
            var viewModel = new MCPServerEditorViewModel(SelectedServer);
            // dialog.DataContext = viewModel; // Uncomment when the dialog view is implemented
            
            if (dialogResult) // Simulate dialog result for now
            {
                // Get the original enabled state
                bool wasEnabled = SelectedServer.IsEnabled;
                
                // Update server from the editor
                SelectedServer.Name = viewModel.Name;
                SelectedServer.Url = viewModel.Url;
                SelectedServer.Type = viewModel.Type;
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
                // This is a simplified version that simulates testing the connection
                // In a real implementation, you would create the appropriate server client
                // and call its IsAvailableAsync method
                await Task.Delay(1000); // Simulate network delay
                
                // Simulate a successful connection
                SelectedServer.IsConnected = true;
                SelectedServer.LastConnectionAttempt = DateTime.Now;
                SelectedServer.AvailableToolCount = new Random().Next(1, 10); // Simulate tools
            }
            catch (Exception ex)
            {
                SelectedServer.IsConnected = false;
                SelectedServer.ConnectionError = ex.Message;
            }
            finally
            {
                SelectedServer.IsConnecting = false;
            }
        }
        
        /// <summary>
        /// Can test the connection to the selected server
        /// </summary>
        private bool CanTestConnection() => SelectedServer != null;
    }
}