using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using AIAgentTest.Commands;
using AIAgentTest.Services;
using AIAgentTest.Services.Interfaces;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ILLMClientService _llmClientService;
        private readonly IMCPContextManager _contextManager;
        
        private int _maxTokens = 1024;
        private double _temperature = 0.7;
        private bool _useContext = true;
        private bool _enableMCP = true;
        private int _historyDepth = 5;
        private int _toolTimeout = 30;
        private int _maxContextLength = 4096;
        private int _maxResponseLength = 2048; 
        private double _topP = 0.9;
        private double _frequencyPenalty = 0.0;
        private double _presencePenalty = 0.0;
        
        public int MaxTokens
        {
            get => _maxTokens;
            set => SetProperty(ref _maxTokens, value);
        }
        
        public double Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, Math.Clamp(value, 0.0, 1.0));
        }
        
        public bool UseContext
        {
            get => _useContext;
            set => SetProperty(ref _useContext, value);
        }
        
        public bool EnableMCP
        {
            get => _enableMCP;
            set => SetProperty(ref _enableMCP, value);
        }
        
        public int HistoryDepth
        {
            get => _historyDepth;
            set => SetProperty(ref _historyDepth, Math.Max(1, value));
        }
        
        public int ToolTimeout
        {
            get => _toolTimeout;
            set => SetProperty(ref _toolTimeout, Math.Max(5, value));
        }
        
        public int MaxContextLength
        {
            get => _maxContextLength;
            set => SetProperty(ref _maxContextLength, value);
        }
        
        public int MaxResponseLength
        {
            get => _maxResponseLength;
            set => SetProperty(ref _maxResponseLength, value);
        }
        
        public double TopP
        {
            get => _topP;
            set => SetProperty(ref _topP, Math.Clamp(value, 0.0, 1.0));
        }
        
        public double FrequencyPenalty
        {
            get => _frequencyPenalty;
            set => SetProperty(ref _frequencyPenalty, Math.Clamp(value, 0.0, 2.0));
        }
        
        public double PresencePenalty
        {
            get => _presencePenalty;
            set => SetProperty(ref _presencePenalty, Math.Clamp(value, 0.0, 2.0));
        }
        
        public bool EnableToolCalls
        {
            get => EnableMCP;
            set => EnableMCP = value;
        }
        
        public bool EnableContextSummarization
        {
            get => UseContext;
            set => UseContext = value;
        }
        
        public int SummarizationThreshold
        {
            get => HistoryDepth;
            set => HistoryDepth = value;
        }
        
        public ICommand SaveSettingsCommand { get; }
        public ICommand ResetSettingsCommand { get; }
        
        public SettingsViewModel(ILLMClientService llmClientService, IMCPContextManager contextManager)
        {
            _llmClientService = llmClientService ?? throw new ArgumentNullException(nameof(llmClientService));
            _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
            
            // Initialize from settings
            LoadSettings();
            
            // Commands
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            ResetSettingsCommand = new RelayCommand(ResetSettings);
        }
        
        private void LoadSettings()
        {
            // Load from settings
            MaxResponseLength = Properties.Settings.Default.MaxResponseLength;
            Temperature = Properties.Settings.Default.Temperature;
            EnableContextSummarization = Properties.Settings.Default.EnableContextSummarization;
            EnableToolCalls = Properties.Settings.Default.EnableToolCalls;
            SummarizationThreshold = Properties.Settings.Default.SummarizationThreshold;
            ToolTimeout = Properties.Settings.Default.ToolTimeout;
            MaxContextLength = Properties.Settings.Default.MaxContextLength;
            TopP = Properties.Settings.Default.TopP;
            FrequencyPenalty = Properties.Settings.Default.FrequencyPenalty;
            PresencePenalty = Properties.Settings.Default.PresencePenalty;
        }
        
        private void SaveSettings()
        {
            // Apply settings to services
            _contextManager.IsContextEnabled = UseContext;
            
            // Save to user settings
            Properties.Settings.Default.MaxResponseLength = MaxResponseLength;
            Properties.Settings.Default.Temperature = Temperature;
            Properties.Settings.Default.EnableContextSummarization = EnableContextSummarization;
            Properties.Settings.Default.EnableToolCalls = EnableToolCalls;
            Properties.Settings.Default.SummarizationThreshold = SummarizationThreshold;
            Properties.Settings.Default.ToolTimeout = ToolTimeout;
            Properties.Settings.Default.MaxContextLength = MaxContextLength;
            Properties.Settings.Default.TopP = TopP;
            Properties.Settings.Default.FrequencyPenalty = FrequencyPenalty;
            Properties.Settings.Default.PresencePenalty = PresencePenalty;
            Properties.Settings.Default.Save();
            
            MessageBox.Show("Settings saved", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void ResetSettings()
        {
            MaxResponseLength = 2048;
            Temperature = 0.7;
            EnableContextSummarization = true;
            EnableToolCalls = true;
            SummarizationThreshold = 5;
            ToolTimeout = 30;
            MaxContextLength = 4096;
            TopP = 0.9;
            FrequencyPenalty = 0.0;
            PresencePenalty = 0.0;
        }
    }
}