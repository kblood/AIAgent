using System;
using System.Diagnostics;
using AIAgentTest.Services.Interfaces;
using AIAgentTest.ViewModels;

namespace AIAgentTest.Services
{
    /// <summary>
    /// Implementation of IDebugLogger that logs to the DebugViewModel
    /// </summary>
    public class DebugLogger : IDebugLogger
    {
        private readonly DebugViewModel _debugViewModel;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="debugViewModel">The debug view model to log to</param>
        public DebugLogger(DebugViewModel debugViewModel)
        {
            _debugViewModel = debugViewModel ?? throw new ArgumentNullException(nameof(debugViewModel));
        }
        
        /// <summary>
        /// Log a message to the debug panel
        /// </summary>
        /// <param name="message">Message to log</param>
        public void Log(string message)
        {
            // Log to debug output
            Debug.WriteLine(message);
            
            // Ensure UI updates happen on UI thread
            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                _debugViewModel.AddLogEntry($"[MCP] {message}");
            });
        }
        
        /// <summary>
        /// Clear all log entries
        /// </summary>
        public void Clear()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() => {
                _debugViewModel.ClearLog();
            });
        }
    }
}
