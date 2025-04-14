using AIAgentTest.Commands;
using AIAgentTest.Services.MCP;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel for testing MCP tools directly from the UI
    /// </summary>
    public class ToolTestingViewModel : ViewModelBase
    {
        private readonly IToolRegistry _toolRegistry;
        private readonly DebugViewModel _debugViewModel;
        
        private ObservableCollection<ToolDefinition> _availableTools;
        private ToolDefinition _selectedTool;
        private string _inputParameters;
        private string _outputResult;
        private bool _isExecuting;
        private List<string> _recentInputs = new List<string>();
        private int _currentInputIndex = -1;
        
        /// <summary>
        /// Available tools from the registry
        /// </summary>
        public ObservableCollection<ToolDefinition> AvailableTools
        {
            get => _availableTools;
            set => SetProperty(ref _availableTools, value);
        }
        
        /// <summary>
        /// Selected tool to test
        /// </summary>
        public ToolDefinition SelectedTool
        {
            get => _selectedTool;
            set
            {
                if (SetProperty(ref _selectedTool, value))
                {
                    GenerateDefaultInput();
                    OnPropertyChanged(nameof(ToolSchema));
                    OnPropertyChanged(nameof(ToolName));
                    OnPropertyChanged(nameof(ToolDescription));
                    OnPropertyChanged(nameof(HasSelectedTool));
                }
            }
        }
        
        /// <summary>
        /// Input parameters as JSON
        /// </summary>
        public string InputParameters
        {
            get => _inputParameters;
            set => SetProperty(ref _inputParameters, value);
        }
        
        /// <summary>
        /// Output result as JSON
        /// </summary>
        public string OutputResult
        {
            get => _outputResult;
            set => SetProperty(ref _outputResult, value);
        }
        
        /// <summary>
        /// Whether a tool execution is in progress
        /// </summary>
        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                if (SetProperty(ref _isExecuting, value))
                {
                    ((RelayCommand)ExecuteToolCommand).RaiseCanExecuteChanged();
                }
            }
        }
        
        /// <summary>
        /// Whether a tool is selected
        /// </summary>
        public bool HasSelectedTool => _selectedTool != null;
        
        /// <summary>
        /// Schema of the selected tool
        /// </summary>
        public string ToolSchema => _selectedTool?.Schema;
        
        /// <summary>
        /// Name of the selected tool
        /// </summary>
        public string ToolName => _selectedTool?.Name;
        
        /// <summary>
        /// Description of the selected tool
        /// </summary>
        public string ToolDescription => _selectedTool?.Description;
        
        // Commands
        public RelayCommand ExecuteToolCommand { get; }
        public ICommand ClearOutputCommand { get; }
        public ICommand RefreshToolsCommand { get; }
        public ICommand PreviousInputCommand { get; }
        public ICommand NextInputCommand { get; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="toolRegistry">Tool registry</param>
        /// <param name="debugViewModel">Debug view model for logging</param>
        public ToolTestingViewModel(IToolRegistry toolRegistry, DebugViewModel debugViewModel = null)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _debugViewModel = debugViewModel;
            
            // Initialize properties
            _availableTools = new ObservableCollection<ToolDefinition>();
            _inputParameters = "{}";
            _outputResult = "";
            
            // Initialize commands
            ExecuteToolCommand = new RelayCommand(() => ExecuteTool(), () => CanExecuteTool());
            ClearOutputCommand = new RelayCommand(() => ClearOutput());
            RefreshToolsCommand = new RelayCommand(() => RefreshTools());
            PreviousInputCommand = new RelayCommand(() => PreviousInput(), () => CanPreviousInput());
            NextInputCommand = new RelayCommand(() => NextInput(), () => CanNextInput());
            
            // Load tools
            LoadTools();
        }
        
        /// <summary>
        /// Load available tools from the registry
        /// </summary>
        private void LoadTools()
        {
            try
            {
                LogDebug("Loading available tools...");
                
                _availableTools.Clear();
                
                // Get all tools from the registry
                var tools = _toolRegistry.GetAllTools();
                
                // Add tools to the collection
                foreach (var tool in tools.OrderBy(t => t.Name))
                {
                    _availableTools.Add(tool);
                }
                
                LogDebug($"Loaded {_availableTools.Count} tools.");
            }
            catch (Exception ex)
            {
                LogDebug($"Error loading tools: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Generate default input parameters for the selected tool
        /// </summary>
        private void GenerateDefaultInput()
        {
            if (_selectedTool == null)
            {
                InputParameters = "{}";
                return;
            }
            
            try
            {
                LogDebug($"Generating default input for tool: {_selectedTool.Name}");
                
                // Parse the schema
                var schemaObj = JsonDocument.Parse(_selectedTool.Schema).RootElement;
                
                // Create default input object
                var defaultInput = new Dictionary<string, object>();
                
                // Check if schema has properties
                if (schemaObj.TryGetProperty("properties", out var properties))
                {
                    // Iterate through properties
                    foreach (var property in properties.EnumerateObject())
                    {
                        string propertyName = property.Name;
                        var propertyValue = property.Value;
                        
                        // Get property type
                        string propertyType = "string";
                        if (propertyValue.TryGetProperty("type", out var typeElement))
                        {
                            propertyType = typeElement.GetString();
                        }
                        
                        // Set default value based on type
                        switch (propertyType)
                        {
                            case "string":
                                // Check if this is a path property
                                if (propertyName.Contains("path") || propertyName.Contains("dir"))
                                {
                                    defaultInput[propertyName] = "C:\\";
                                }
                                else
                                {
                                    defaultInput[propertyName] = "";
                                }
                                break;
                                
                            case "number":
                            case "integer":
                                defaultInput[propertyName] = 0;
                                break;
                                
                            case "boolean":
                                defaultInput[propertyName] = false;
                                break;
                                
                            case "array":
                                defaultInput[propertyName] = new object[0];
                                break;
                                
                            case "object":
                                defaultInput[propertyName] = new Dictionary<string, object>();
                                break;
                                
                            default:
                                defaultInput[propertyName] = null;
                                break;
                        }
                    }
                }
                
                // Convert to JSON
                InputParameters = JsonSerializer.Serialize(defaultInput, new JsonSerializerOptions { WriteIndented = true });
                
                LogDebug("Default input generated.");
            }
            catch (Exception ex)
            {
                LogDebug($"Error generating default input: {ex.Message}");
                InputParameters = "{}";
            }
        }
        
        /// <summary>
        /// Execute the selected tool with the input parameters
        /// </summary>
        private async void ExecuteTool()
        {
            if (_selectedTool == null)
            {
                return;
            }
            
            try
            {
                IsExecuting = true;
                LogDebug($"Executing tool: {_selectedTool.Name}");
                
                // Parse input parameters
                var inputObj = JsonSerializer.Deserialize<object>(InputParameters);
                
                // Add to recent inputs
                AddToRecentInputs(InputParameters);
                
                // Get tool handler
                var handler = _toolRegistry.GetToolHandler(_selectedTool.Name);
                if (handler == null)
                {
                    LogDebug($"Tool handler not found: {_selectedTool.Name}");
                    OutputResult = "Error: Tool handler not found.";
                    return;
                }
                
                // Execute tool
                var startTime = DateTime.Now;
                var result = await handler(inputObj);
                var endTime = DateTime.Now;
                var executionTime = (endTime - startTime).TotalMilliseconds;
                
                // Format result
                var resultStr = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                
                // Update output
                StringBuilder output = new StringBuilder();
                output.AppendLine($"// Tool: {_selectedTool.Name}");
                output.AppendLine($"// Execution time: {executionTime:0.00} ms");
                output.AppendLine($"// Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                output.AppendLine();
                output.Append(resultStr);
                
                OutputResult = output.ToString();
                
                LogDebug($"Tool executed successfully in {executionTime:0.00} ms.");
            }
            catch (Exception ex)
            {
                LogDebug($"Error executing tool: {ex.Message}");
                OutputResult = $"Error: {ex.Message}";
            }
            finally
            {
                IsExecuting = false;
            }
        }
        
        /// <summary>
        /// Clear the output result
        /// </summary>
        private void ClearOutput()
        {
            OutputResult = "";
        }
        
        /// <summary>
        /// Refresh the list of available tools
        /// </summary>
        private void RefreshTools()
        {
            LoadTools();
        }
        
        /// <summary>
        /// Add input to recent inputs history
        /// </summary>
        private void AddToRecentInputs(string input)
        {
            // Add to the list
            _recentInputs.Remove(input); // Remove if already exists
            _recentInputs.Add(input); // Add to the end
            
            // Limit to 10 most recent
            while (_recentInputs.Count > 10)
            {
                _recentInputs.RemoveAt(0);
            }
            
            // Set current index to the end
            _currentInputIndex = _recentInputs.Count;
            
            // Update command can execute state
            ((RelayCommand)PreviousInputCommand).RaiseCanExecuteChanged();
            ((RelayCommand)NextInputCommand).RaiseCanExecuteChanged();
        }
        
        /// <summary>
        /// Go to previous input in history
        /// </summary>
        private void PreviousInput()
        {
            if (_recentInputs.Count == 0 || _currentInputIndex <= 0)
            {
                return;
            }
            
            _currentInputIndex--;
            InputParameters = _recentInputs[_currentInputIndex];
            
            ((RelayCommand)PreviousInputCommand).RaiseCanExecuteChanged();
            ((RelayCommand)NextInputCommand).RaiseCanExecuteChanged();
        }
        
        /// <summary>
        /// Go to next input in history
        /// </summary>
        private void NextInput()
        {
            if (_recentInputs.Count == 0 || _currentInputIndex >= _recentInputs.Count - 1)
            {
                return;
            }
            
            _currentInputIndex++;
            InputParameters = _recentInputs[_currentInputIndex];
            
            PreviousInputCommand.RaiseCanExecuteChanged();
            NextInputCommand.RaiseCanExecuteChanged();
        }
        
        /// <summary>
        /// Check if the tool can be executed
        /// </summary>
        private bool CanExecuteTool()
        {
            return _selectedTool != null && !_isExecuting;
        }
        
        /// <summary>
        /// Check if there is a previous input available
        /// </summary>
        private bool CanPreviousInput()
        {
            return _recentInputs.Count > 0 && _currentInputIndex > 0;
        }
        
        /// <summary>
        /// Check if there is a next input available
        /// </summary>
        private bool CanNextInput()
        {
            return _recentInputs.Count > 0 && _currentInputIndex < _recentInputs.Count - 1;
        }
        
        /// <summary>
        /// Log a debug message
        /// </summary>
        private void LogDebug(string message)
        {
            _debugViewModel?.Log($"ToolTesting: {message}");
        }
    }
}