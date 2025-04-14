using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AIAgentTest.ViewModels;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Logger for tool operations to track filesystem access and operations
    /// </summary>
    public class ToolOperationLogger
    {
        private readonly string _logDirectory;
        private readonly string _logFilePath;
        private readonly object _lockObject = new object();
        private readonly int _maxLogEntries;
        private readonly bool _enableFileLogging;
        private readonly List<ToolOperationLogEntry> _recentOperations;
        private readonly DebugViewModel _debugViewModel;
        
        /// <summary>
        /// Recent operations performed by tools
        /// </summary>
        public IReadOnlyList<ToolOperationLogEntry> RecentOperations => _recentOperations.AsReadOnly();
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logDirectory">Directory to store log files</param>
        /// <param name="maxLogEntries">Maximum number of log entries to keep in memory</param>
        /// <param name="enableFileLogging">Whether to log to files</param>
        /// <param name="debugViewModel">Debug ViewModel for logging</param>
        public ToolOperationLogger(string logDirectory = null, int maxLogEntries = 100, bool enableFileLogging = true, DebugViewModel debugViewModel = null)
        {
            _maxLogEntries = maxLogEntries;
            _enableFileLogging = enableFileLogging;
            _debugViewModel = debugViewModel;
            _recentOperations = new List<ToolOperationLogEntry>(_maxLogEntries);
            
            if (enableFileLogging)
            {
                try
                {
                    // Set up log directory
                    if (string.IsNullOrEmpty(logDirectory))
                    {
                        _logDirectory = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "AIAgent",
                            "Logs");
                    }
                    else
                    {
                        _logDirectory = logDirectory;
                    }
                    
                    // Create directory if it doesn't exist
                    if (!Directory.Exists(_logDirectory))
                    {
                        Directory.CreateDirectory(_logDirectory);
                    }
                    
                    // Set up log file path
                    string dateString = DateTime.Now.ToString("yyyyMMdd");
                    _logFilePath = Path.Combine(_logDirectory, $"ToolOperations_{dateString}.json");
                    
                    LogDebug($"Tool operation logger initialized. Log file: {_logFilePath}");
                }
                catch (Exception ex)
                {
                    LogDebug($"Error initializing tool operation logger: {ex.Message}");
                    _enableFileLogging = false;
                }
            }
            else
            {
                LogDebug("Tool operation logger initialized with file logging disabled.");
            }
        }
        
        /// <summary>
        /// Log a tool operation
        /// </summary>
        /// <param name="toolName">Name of the tool</param>
        /// <param name="input">Input parameters</param>
        /// <param name="result">Operation result</param>
        /// <param name="success">Whether the operation succeeded</param>
        /// <param name="errorMessage">Error message if the operation failed</param>
        /// <returns>The created log entry</returns>
        public async Task<ToolOperationLogEntry> LogOperationAsync(
            string toolName, 
            object input, 
            object result = null, 
            bool success = true, 
            string errorMessage = null)
        {
            var entry = new ToolOperationLogEntry
            {
                Timestamp = DateTime.Now,
                ToolName = toolName,
                Input = input,
                Result = result,
                Success = success,
                ErrorMessage = errorMessage
            };
            
            // Add to recent operations
            lock (_lockObject)
            {
                _recentOperations.Add(entry);
                
                // Trim to max size
                while (_recentOperations.Count > _maxLogEntries)
                {
                    _recentOperations.RemoveAt(0);
                }
            }
            
            // Log to file if enabled
            if (_enableFileLogging)
            {
                await WriteToLogFileAsync(entry);
            }
            
            LogDebug($"Logged {toolName} operation. Success: {success}");
            
            return entry;
        }
        
        /// <summary>
        /// Write a log entry to the log file
        /// </summary>
        /// <param name="entry">Log entry to write</param>
        private async Task WriteToLogFileAsync(ToolOperationLogEntry entry)
        {
            try
            {
                // Convert to JSON
                var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                // Write to file
                await File.AppendAllTextAsync(_logFilePath, json + ",\n");
            }
            catch (Exception ex)
            {
                LogDebug($"Error writing to log file: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Clear recent operations
        /// </summary>
        public void ClearRecentOperations()
        {
            lock (_lockObject)
            {
                _recentOperations.Clear();
            }
            
            LogDebug("Cleared recent operations.");
        }
        
        /// <summary>
        /// Get recent operations for a specific tool
        /// </summary>
        /// <param name="toolName">Name of the tool</param>
        /// <returns>List of recent operations for the tool</returns>
        public List<ToolOperationLogEntry> GetRecentOperationsForTool(string toolName)
        {
            lock (_lockObject)
            {
                return _recentOperations.FindAll(op => op.ToolName == toolName);
            }
        }
        
        /// <summary>
        /// Log a debug message
        /// </summary>
        private void LogDebug(string message)
        {
            _debugViewModel?.Log($"ToolOperationLogger: {message}");
        }
    }
    
    /// <summary>
    /// Log entry for a tool operation
    /// </summary>
    public class ToolOperationLogEntry
    {
        /// <summary>
        /// Timestamp of the operation
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Name of the tool
        /// </summary>
        public string ToolName { get; set; }
        
        /// <summary>
        /// Input parameters
        /// </summary>
        public object Input { get; set; }
        
        /// <summary>
        /// Operation result
        /// </summary>
        public object Result { get; set; }
        
        /// <summary>
        /// Whether the operation succeeded
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>
        /// Format the operation as a string
        /// </summary>
        public override string ToString()
        {
            return $"{Timestamp:yyyy-MM-dd HH:mm:ss} - {ToolName} - {(Success ? "Success" : "Error")}" +
                   (ErrorMessage != null ? $" - {ErrorMessage}" : "");
        }
    }
}