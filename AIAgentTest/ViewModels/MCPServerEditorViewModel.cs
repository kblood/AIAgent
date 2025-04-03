namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel for editing an MCP server
    /// </summary>
    public class MCPServerEditorViewModel : ViewModelBase
    {
        private string _name;
        private string _url;
        private string _type;
        private bool _isEnabled;
        
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
        /// Available server types
        /// </summary>
        public string[] ServerTypes { get; } = { "filesystem", "database", "custom" };
        
        /// <summary>
        /// Constructor for creating a new server
        /// </summary>
        public MCPServerEditorViewModel(MCPServerViewModel server)
        {
            if (server != null)
            {
                // Editing existing server
                Name = server.Name;
                Url = server.Url;
                Type = server.Type;
                IsEnabled = server.IsEnabled;
            }
            else
            {
                // Creating new server
                Name = "New Server";
                Url = "http://localhost:3000";
                Type = "filesystem";
                IsEnabled = true;
            }
        }
    }
}
