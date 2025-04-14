using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private IContextManager _contextManager;
        
        private bool _isVisible = true;
        private string _debugContent;
        private string _technicalContext;
    private ObservableCollection<string> _logEntries;
    
        
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
    
    public ObservableCollection<string> LogEntries
    {
        get => _logEntries;
        set => SetProperty(ref _logEntries, value);
    }
        
        // Commands
        public ICommand ShowContextCommand { get; }
        public ICommand ClearContextCommand { get; }
        public ICommand SummarizeContextCommand { get; }
    public ICommand ClearLogsCommand { get; }
        
        public DebugViewModel(IContextManager contextManager)
        {
        _contextManager = contextManager; // Allow null for initialization; will be set later if necessary
        _logEntries = new ObservableCollection<string>();
        
            // Initialize with default values
            _isVisible = true;
            _debugContent = "Debug window is initializing...";
            
            // Initialize commands
            ShowContextCommand = new RelayCommand(ShowTechnicalContext);
            ClearContextCommand = new RelayCommand(ClearContext);
            SummarizeContextCommand = new RelayCommand(async () => await SummarizeContext());
        ClearLogsCommand = new RelayCommand(() => ClearLog());
            
            // Show initial debug info safely
            try
            {
            DebugContent = $"Debug window initialized at {DateTime.Now}.\n\n";
            
            if (_contextManager != null)
            {
                DebugContent += $"Context Manager Type: {_contextManager.GetType().Name}\n\n";
                DebugContent += _contextManager.GetDebugInfo();
                
            // Check for MCPContextManager specifically
                if (_contextManager is IMCPContextManager)
                    {
                    DebugContent += "\n\n[MCP Context Manager detected]";
                }
            }
            else
            {
                DebugContent += "Context Manager not initialized yet.";
            }
            
            // Add first log entry
            AddLogEntry("Debug logging initialized");
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
                else if (_contextManager != null)
                {
                    // Fallback to getting context from the context manager
                    string context = _contextManager.GetFullContext();
                    contextBuilder.AppendLine(context);
                }
                else
                {
                    contextBuilder.AppendLine("No context available - context manager not initialized.");
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
                else if (_contextManager != null)
                {
                    // Add basic token statistics
                    contextBuilder.AppendLine("\n=== TOKEN STATISTICS ===\n");
                    int totalTokens = TokenCounterUtility.EstimateTokenCount(contextBuilder.ToString());
                    contextBuilder.AppendLine($"Approximate Context Size: {totalTokens} tokens");
                }
                else
                {
                    contextBuilder.AppendLine("\n=== TOKEN STATISTICS ===\n");
                    contextBuilder.AppendLine("No statistics available - context manager not initialized.");
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
        if (_contextManager != null)
        {
                _contextManager.ClearContext();
                DebugContent = "Context cleared.";
            }
        else
            {
                    DebugContent = "Context Manager not initialized yet.";
            }
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
            AddLogEntry("Attempting to summarize context...");
            
            if (_contextManager == null)
            {
                DebugContent = "Context Manager not initialized yet.";
                AddLogEntry("Error: Context Manager not initialized yet.");
                
                // Try to get it from the service provider
                var contextManager = ServiceProvider.GetService<IContextManager>();
                if (contextManager != null)
                {
                    AddLogEntry("Found context manager in service provider. Updating reference.");
                    _contextManager = contextManager;
                    DebugContent = $"Context Manager found: {_contextManager.GetType().Name}";
                }
                else
                {
                    AddLogEntry("Could not find context manager in service provider.");
                    return;
                }
            }

            // Check if we have a regular context manager or an MCP context manager
            if (_contextManager is IMCPContextManager mcpContextManager)
            {
                // For MCP context manager, show more detailed status
                DebugContent = "Summarizing MCP context...";
                AddLogEntry("Using MCP context manager");
                await mcpContextManager.SummarizeContext(mcpContextManager.DefaultModel ?? "llama3");
                DebugContent = "Context has been summarized. Here's the updated context:\n\n";
                DebugContent += mcpContextManager.GetFullContext();
            }
            else
            {
                // For regular context manager
                DebugContent = "Summarizing context...";
                AddLogEntry("Using standard context manager");
                await _contextManager.SummarizeContext(_contextManager.DefaultModel ?? "llama3");
                DebugContent = _contextManager.GetDebugInfo();
            }
            
            AddLogEntry("Context summarization completed successfully");
        }
        catch (Exception ex)
        {
            DebugContent = $"Error summarizing context: {ex.Message}\n{ex.StackTrace}";
            AddLogEntry($"Error summarizing context: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Add a log entry to the debug panel
    /// </summary>
    /// <param name="entry">Log entry text</param>
    public void AddLogEntry(string entry)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        System.Windows.Application.Current.Dispatcher.Invoke(() => {
            LogEntries.Add($"[{timestamp}] {entry}");
            
            // Limit log size to prevent performance issues
            if (LogEntries.Count > 1000)
            {
                LogEntries.RemoveAt(0);
            }
        });
    }
    
    /// <summary>
    /// Log a message to the debug panel
    /// </summary>
    /// <param name="message">Message to log</param>
    public void Log(string message)
    {
        AddLogEntry(message);
    }
    
    /// <summary>
    /// Clear all log entries
    /// </summary>
    public void ClearLog()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() => {
            LogEntries.Clear();
            AddLogEntry("Log cleared");
        });
    }
    
    /// <summary>
    /// Update the context manager reference
    /// </summary>
    public IContextManager ContextManager
    {
        get => _contextManager;
        set => _contextManager = value;
    }
    }
}