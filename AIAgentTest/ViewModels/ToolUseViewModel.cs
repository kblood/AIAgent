using System.Collections.Generic;
using System.Text.Json;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel for representing tool usage in the UI
    /// </summary>
    public class ToolUseViewModel : ViewModelBase
    {
        private string _toolName;
        private Dictionary<string, object> _input;
        private object _result;
        private bool _isExecuting;
        private bool _succeeded;
        private string _error;
        
        /// <summary>
        /// Name of the tool
        /// </summary>
        public string ToolName
        {
            get => _toolName;
            set => SetProperty(ref _toolName, value);
        }
        
        /// <summary>
        /// Input parameters for the tool
        /// </summary>
        public Dictionary<string, object> Input
        {
            get => _input;
            set => SetProperty(ref _input, value);
        }
        
        /// <summary>
        /// Result from executing the tool
        /// </summary>
        public object Result
        {
            get => _result;
            set => SetProperty(ref _result, value);
        }
        
        /// <summary>
        /// Whether the tool is currently executing
        /// </summary>
        public bool IsExecuting
        {
            get => _isExecuting;
            set => SetProperty(ref _isExecuting, value);
        }
        
        /// <summary>
        /// Whether the tool execution succeeded
        /// </summary>
        public bool Succeeded
        {
            get => _succeeded;
            set => SetProperty(ref _succeeded, value);
        }
        
        /// <summary>
        /// Error message if the tool execution failed
        /// </summary>
        public string Error
        {
            get => _error;
            set => SetProperty(ref _error, value);
        }
        
        /// <summary>
        /// Gets the input parameters formatted as JSON
        /// </summary>
        public string GetFormattedInput()
        {
            return JsonSerializer.Serialize(Input, new JsonSerializerOptions { WriteIndented = true });
        }
        
        /// <summary>
        /// Gets the result formatted as JSON
        /// </summary>
        public string GetFormattedResult()
        {
            if (Result == null) return string.Empty;
            return JsonSerializer.Serialize(Result, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}