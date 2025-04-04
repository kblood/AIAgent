using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using AIAgentTest.Commands;
using AIAgentTest.Services;
using AIAgentTest.Services.Interfaces;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.ViewModels
{
    public class DebugViewModel : ViewModelBase
    {
        private readonly IContextManager _contextManager;
        
        private bool _isVisible = true;
        private string _debugContent;
        private string _technicalContext;
        
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
        
        public string TechnicalContext
        {
            get => _technicalContext;
            set => SetProperty(ref _technicalContext, value);
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
            ShowContextCommand = new RelayCommand(ShowTechnicalContext);
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
        
private void ShowTechnicalContext()
        {
            try 
            {
                var contextBuilder = new StringBuilder();
                contextBuilder.AppendLine("=== FULL CURRENT CONTEXT ===\n");

                // Get the ChatSessionViewModel to access its data
                var chatVM = ServiceProvider.GetService<ChatSessionViewModel>();
                if (chatVM != null && chatVM.CurrentSession != null && chatVM.CurrentSession.Messages != null)
                {
                    // Show messages directly from the current session
                    foreach (var message in chatVM.CurrentSession.Messages)
                    {
                        // Add the basic message
                        contextBuilder.AppendLine($"{message.Role}: {message.Content}");
                        
                        // Check for tool information in metadata
                        if (message.Metadata != null)
                        {
                            // Tool call code
                            if (message.Metadata.ContainsKey("ToolCallJson"))
                            {
                                string toolName = message.Metadata.ContainsKey("ToolName") 
                                    ? message.Metadata["ToolName"].ToString()
                                    : "unknown";
                                    
                                contextBuilder.AppendLine($"--- TOOL CALL: {toolName} ---");
                                contextBuilder.AppendLine(message.Metadata["ToolCallJson"].ToString());
                            }
                            
                            // Tool result code
                            if (message.Metadata.ContainsKey("ToolResultJson"))
                            {
                                string toolName = message.Metadata.ContainsKey("ToolName") 
                                    ? message.Metadata["ToolName"].ToString()
                                    : "unknown";
                                    
                                contextBuilder.AppendLine($"--- TOOL RESULT: {toolName} ---");
                                contextBuilder.AppendLine(message.Metadata["ToolResultJson"].ToString());
                            }
                            
                            // Full tool interaction if available
                            if (message.Metadata.ContainsKey("FullToolInteraction") && 
                                !message.Metadata.ContainsKey("ToolCallJson") && 
                                !message.Metadata.ContainsKey("ToolResultJson"))
                            {
                                contextBuilder.AppendLine(message.Metadata["FullToolInteraction"].ToString());
                            }
                        }
                        
                        // Add a blank line after each message for readability
                        contextBuilder.AppendLine();
                    }
                }
                else
                {
                    // Fallback to getting context from the context manager
                    string context = _contextManager.GetFullContext();
                    contextBuilder.AppendLine(context);
                }
                
                // Add token statistics
                if (_contextManager is IMCPContextManager mcpContextManager)
                {
                    contextBuilder.AppendLine("\n=== TOOL CALLS ANALYSIS ===\n");
                    contextBuilder.AppendLine(mcpContextManager.GetToolUsageInfo());
                    
                    // Add token statistics
                    contextBuilder.AppendLine("\n=== TOKEN STATISTICS ===\n");
                    contextBuilder.AppendLine(mcpContextManager.GetTokenStatistics());
                }
                else
                {
                    // Add basic token statistics
                    contextBuilder.AppendLine("\n=== TOKEN STATISTICS ===\n");
                    int totalTokens = TokenCounterUtility.EstimateTokenCount(contextBuilder.ToString());
                    contextBuilder.AppendLine($"Approximate Context Size: {totalTokens} tokens");
                }
                
                TechnicalContext = contextBuilder.ToString();
                DebugContent = TechnicalContext;
            }
            catch (Exception ex)
            {
                DebugContent = $"Error getting technical context: {ex.Message}\n{ex.StackTrace}";
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