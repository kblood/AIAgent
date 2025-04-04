using System;
using System.Threading.Tasks;
using System.Windows.Input;
using AIAgentTest.Commands;
using AIAgentTest.Services.Interfaces;

namespace AIAgentTest.ViewModels
{
    public class DebugViewModel : ViewModelBase
    {
        private readonly IContextManager _contextManager;
        
        private bool _isVisible = true;
        private string _debugContent;
        
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }
        
        public string DebugContent
        {
            get => _debugContent;
            set => SetProperty(ref _debugContent, value);
        }
        
        // Commands
        public ICommand ShowContextCommand { get; }
        public ICommand ClearContextCommand { get; }
        public ICommand SummarizeContextCommand { get; }
        
        public DebugViewModel(IContextManager contextManager)
        {
            _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
            
            // Initialize with default values
            _isVisible = true;
            _debugContent = "Debug window is initializing...";
            
            // Initialize commands
            ShowContextCommand = new RelayCommand(ShowContext);
            ClearContextCommand = new RelayCommand(ClearContext);
            SummarizeContextCommand = new RelayCommand(async () => await SummarizeContext());
            
            // Show initial debug info safely
            try
            {
                DebugContent = $"Debug window initialized at {DateTime.Now}.\n\n";
                DebugContent += $"Context Manager Type: {_contextManager.GetType().Name}\n\n";
                DebugContent += _contextManager.GetDebugInfo();
                
                // Check for MCPContextManager specifically
                if (_contextManager is IMCPContextManager)
                {
                    DebugContent += "\n\n[MCP Context Manager detected]";
                }
            }
            catch (Exception ex)
            {
                DebugContent = $"Error initializing debug window: {ex.Message}\n{ex.StackTrace}";
            }
        }
        
        private void ShowContext()
        {
            try 
            {
                string context = _contextManager.GetFullContext();
                DebugContent = string.IsNullOrEmpty(context) ? "Context is empty." : context;
            }
            catch (Exception ex)
            {
                DebugContent = $"Error getting context: {ex.Message}\n{ex.StackTrace}";
            }
        }
        
        private void ClearContext()
        {
            try
            {
                _contextManager.ClearContext();
                DebugContent = "Context cleared.";
            }
            catch (Exception ex)
            {
                DebugContent = $"Error clearing context: {ex.Message}\n{ex.StackTrace}";
            }
        }
        
        private async Task SummarizeContext()
        {
            try
            {
                // Check if we have a regular context manager or an MCP context manager
                if (_contextManager is IMCPContextManager mcpContextManager)
                {
                    // For MCP context manager, show more detailed status
                    DebugContent = "Summarizing MCP context...";
                    await mcpContextManager.SummarizeContext(mcpContextManager.DefaultModel);
                    DebugContent = "Context has been summarized. Here's the updated context:\n\n";
                    DebugContent += mcpContextManager.GetFullContext();
                }
                else
                {
                    // For regular context manager
                    DebugContent = "Summarizing context...";
                    await _contextManager.SummarizeContext(_contextManager.DefaultModel);
                    DebugContent = _contextManager.GetDebugInfo();
                }
            }
            catch (Exception ex)
            {
                DebugContent = $"Error summarizing context: {ex.Message}\n{ex.StackTrace}";
            }
        }
    }
}