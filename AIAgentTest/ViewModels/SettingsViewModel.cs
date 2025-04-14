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
            MaxTokens = Properties.Settings.Default.MaxResponseLength;
            Temperature = Properties.Settings.Default.Temperature;
            UseContext = Properties.Settings.Default.EnableContextSummarization;
            EnableMCP = Properties.Settings.Default.EnableToolCalls;
            HistoryDepth = Properties.Settings.Default.SummarizationThreshold;
        }
        
        private void SaveSettings()
        {
            // Apply settings to services
            _contextManager.IsContextEnabled = UseContext;
            
            // Save to user settings
            Properties.Settings.Default.MaxResponseLength = MaxTokens;
            Properties.Settings.Default.Temperature = Temperature;
            Properties.Settings.Default.EnableContextSummarization = UseContext;
            Properties.Settings.Default.EnableToolCalls = EnableMCP;
            Properties.Settings.Default.SummarizationThreshold = HistoryDepth;
            Properties.Settings.Default.Save();
            
            MessageBox.Show("Settings saved", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void ResetSettings()
        {
            MaxTokens = 1024;
            Temperature = 0.7;
            UseContext = true;
            EnableMCP = true;
            HistoryDepth = 5;
        }
    }
}