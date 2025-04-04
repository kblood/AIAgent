using System;
using System.Windows.Input;
using AIAgentTest.Commands;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel for toggling a tool on/off
    /// </summary>
    public class ToolToggleViewModel : ViewModelBase
    {
        private readonly IToolRegistry _toolRegistry;
        private readonly ToolDefinition _toolDefinition;
        private bool _isEnabled;
        
        /// <summary>
        /// Tool definition
        /// </summary>
        public ToolDefinition ToolDefinition => _toolDefinition;
        
        /// <summary>
        /// Tool name
        /// </summary>
        public string Name => _toolDefinition.Name;
        
        /// <summary>
        /// Tool description
        /// </summary>
        public string Description => _toolDefinition.Description;
        
        /// <summary>
        /// Tool tags/categories
        /// </summary>
        public string[] Tags => _toolDefinition.Tags;
        
        /// <summary>
        /// Whether the tool is enabled
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (SetProperty(ref _isEnabled, value))
                {
                    // Update tool registry
                    if (value)
                    {
                        _toolRegistry.EnableTool(Name);
                    }
                    else
                    {
                        _toolRegistry.DisableTool(Name);
                    }
                }
            }
        }
        
        /// <summary>
        /// Command to toggle the tool
        /// </summary>
        public ICommand ToggleCommand { get; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="toolDefinition">Tool definition</param>
        /// <param name="toolRegistry">Tool registry</param>
        public ToolToggleViewModel(ToolDefinition toolDefinition, IToolRegistry toolRegistry)
        {
            _toolDefinition = toolDefinition ?? throw new ArgumentNullException(nameof(toolDefinition));
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _isEnabled = _toolRegistry.IsToolEnabled(toolDefinition.Name);
            
            ToggleCommand = new RelayCommand(() => IsEnabled = !IsEnabled);
        }
    }
}
