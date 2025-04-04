using System;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel for an MCP server
    /// </summary>
    public class MCPServerViewModel : ViewModelBase
    {
        private string _name;
        private string _command;
        private string[] _args;
        private bool _isEnabled;
        private bool _isConnected;
        private bool _isConnecting;
        private DateTime? _lastConnectionAttempt;
        private int _availableToolCount;
        private string _connectionError;
        
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
    }
}
