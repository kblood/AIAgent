using System;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel for an MCP server
    /// </summary>
    public class MCPServerViewModel : ViewModelBase
    {
        private string _name;
        private string _url;
        private string _type;
        private bool _isEnabled;
        private bool _isConnected;
        private bool _isConnecting;
        private DateTime? _lastConnectionAttempt;
        private string _connectionError;
        private int _availableToolCount;
        
        /// <summary>
        /// Name of the server
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        
        /// <summary>
        /// URL of the server
        /// </summary>
        public string Url
        {
            get => _url;
            set => SetProperty(ref _url, value);
        }
        
        /// <summary>
        /// Type of the server (e.g., "filesystem", "database", "custom")
        /// </summary>
        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
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
        /// Whether the server is currently connecting
        /// </summary>
        public bool IsConnecting
        {
            get => _isConnecting;
            set => SetProperty(ref _isConnecting, value);
        }
        
        /// <summary>
        /// Last time a connection attempt was made
        /// </summary>
        public DateTime? LastConnectionAttempt
        {
            get => _lastConnectionAttempt;
            set => SetProperty(ref _lastConnectionAttempt, value);
        }
        
        /// <summary>
        /// Error message from the last connection attempt
        /// </summary>
        public string ConnectionError
        {
            get => _connectionError;
            set => SetProperty(ref _connectionError, value);
        }
        
        /// <summary>
        /// Number of available tools on the server
        /// </summary>
        public int AvailableToolCount
        {
            get => _availableToolCount;
            set => SetProperty(ref _availableToolCount, value);
        }
    }
}
