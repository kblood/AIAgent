using System;
using System.Threading.Tasks;
using AIAgentTest.API_Clients.MCP;
using AIAgentTest.Services;
using AIAgentTest.Services.Interfaces;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel for an MCP server
    /// </summary>
    public class MCPServerViewModel : ViewModelBase
    {
        private readonly IMCPServerClient _client;
        private string _name;
        private string _command;
        private string[] _args;
        private bool _isEnabled;
        private bool _isConnected;
        private bool _isConnecting;
        private bool _isActive;
        private bool _isRunning;
        private DateTime? _lastConnectionAttempt;
        private int _availableToolCount;
        private string _connectionError;
        private IMCPServerClient _serverClient;
        
        /// <summary>
        /// Server name
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        
        /// <summary>
        /// Server command
        /// </summary>
        public string Command
        {
            get => _command;
            set => SetProperty(ref _command, value);
        }
        
        /// <summary>
        /// Server arguments
        /// </summary>
        public string[] Args
        {
            get => _args;
            set => SetProperty(ref _args, value);
        }
        
        /// <summary>
        /// Server type (derived from command and args)
        /// </summary>
        public string Type
        {
            get
            {
                if (_command == "npx" && _args != null && _args.Length > 0)
                {
                    if (_args[0].Contains("server-filesystem") || 
                        (_args.Length > 1 && _args[1].Contains("server-filesystem")))
                    {
                        return "filesystem";
                    }
                }
                return "custom";
            }
        }
        
        /// <summary>
        /// Gets a display-friendly URL or path from the arguments
        /// </summary>
        public string DisplayPath
        {
            get
            {
                if (_args != null && _args.Length > 0)
                {
                    // For filesystem, the last argument is typically the path
                    return _args.LastOrDefault() ?? "";
                }
                return "";
            }
        }
        
        /// <summary>
        /// Whether the server is enabled
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }
        
        /// <summary>
        /// Whether the server is connected
        /// </summary>
        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }
        
        /// <summary>
        /// Whether the server is running
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }
        
        /// <summary>
        /// The server client
        /// </summary>
        public IMCPServerClient ServerClient
        {
            get => _serverClient;
            set => SetProperty(ref _serverClient, value);
        }
        
        /// <summary>
        /// Whether the server is active (running)
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }
        
        /// <summary>
        /// Whether the server is connecting
        /// </summary>
        public bool IsConnecting
        {
            get => _isConnecting;
            set => SetProperty(ref _isConnecting, value);
        }
        
        /// <summary>
        /// When the server was last checked
        /// </summary>
        public DateTime? LastConnectionAttempt
        {
            get => _lastConnectionAttempt;
            set => SetProperty(ref _lastConnectionAttempt, value);
        }
        
        /// <summary>
        /// Number of available tools
        /// </summary>
        public int AvailableToolCount
        {
            get => _availableToolCount;
            set => SetProperty(ref _availableToolCount, value);
        }
        
        /// <summary>
        /// Connection error message
        /// </summary>
        public string ConnectionError
        {
            get => _connectionError;
            set => SetProperty(ref _connectionError, value);
        }
        
        /// <summary>
        /// Default constructor
        /// </summary>
        public MCPServerViewModel()
        {
            // Default constructor for binding
        }
        
        /// <summary>
        /// Constructor with client
        /// </summary>
        /// <param name="name">Server name</param>
        /// <param name="client">MCP client</param>
        public MCPServerViewModel(string name, IMCPServerClient client)
        {
            Name = name;
            _client = client;
        }
        
        /// <summary>
        /// Check if the server is available
        /// </summary>
        /// <returns>True if available</returns>
        public async Task<bool> IsAvailableAsync()
        {
            if (_client == null) return false;
            return await _client.IsAvailableAsync();
        }
        
        /// <summary>
        /// Start the server
        /// </summary>
        /// <returns>True if started</returns>
        public async Task<bool> StartAsync()
        {
            if (_client == null) return false;
            IsActive = await _client.StartServerAsync();
            return IsActive;
        }
        
        /// <summary>
        /// Stop the server
        /// </summary>
        public void Stop()
        {
            if (_client == null) return;
            _client.StopServer();
            IsActive = false;
        }
    }
}
