using System.Linq;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel representing a tool that can be toggled on or off
    /// </summary>
    public class ToolToggleViewModel : ViewModelBase
    {
        private readonly ToolDefinition _tool;
        private readonly IToolRegistry _toolRegistry;
        private bool _isEnabled;
        
        /// <summary>
        /// Name of the tool
        /// </summary>
        public string Name => _tool.Name;
        
        /// <summary>
        /// Description of the tool
        /// </summary>
        public string Description => _tool.Description;
        
        /// <summary>
        /// Type of the tool
        /// </summary>
        public string Type => _tool.ToolType;
        
        /// <summary>
        /// Category of the tool
        /// </summary>
        public string Category => _tool.Tags.FirstOrDefault() ?? "General";
        
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
                        _toolRegistry.EnableTool(_tool.Name);
                    }
                    else
                    {
                        _toolRegistry.DisableTool(_tool.Name);
                    }
                }
            }
        }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="tool">Tool definition</param>
        /// <param name="toolRegistry">Tool registry</param>
        public ToolToggleViewModel(ToolDefinition tool, IToolRegistry toolRegistry)
        {
            _tool = tool;
            _toolRegistry = toolRegistry;
            
            // Get initial enabled state
            _isEnabled = _toolRegistry.IsToolEnabled(_tool.Name);
        }
    }
}