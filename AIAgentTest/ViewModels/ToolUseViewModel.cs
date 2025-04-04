using System;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel for tool usage in chat
    /// </summary>
    public class ToolUseViewModel : ViewModelBase
    {
        private string _toolName;
        private object _input;
        private object _result;
        private string _error;
        private bool _succeeded;
        private bool _isExecuting;
        
        /// <summary>
        /// Name of the tool being used
        /// </summary>
        public string ToolName
        {
            get => _toolName;
            set => SetProperty(ref _toolName, value);
        }
        
        /// <summary>
        /// Input parameters for the tool
        /// </summary>
        public object Input
        {
            get => _input;
            set => SetProperty(ref _input, value);
        }
        
        /// <summary>
        /// Result of the tool execution
        /// </summary>
        public object Result
        {
            get => _result;
            set => SetProperty(ref _result, value);
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
        /// Whether the tool execution succeeded
        /// </summary>
        public bool Succeeded
        {
            get => _succeeded;
            set => SetProperty(ref _succeeded, value);
        }
        
        /// <summary>
        /// Whether the tool is currently executing
        /// </summary>
        public bool IsExecuting
        {
            get => _isExecuting;
            set => SetProperty(ref _isExecuting, value);
        }
    }
}
