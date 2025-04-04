using System;
using System.Windows.Input;
using AIAgentTest.Commands;
using AIAgentTest.Services;
using AIAgentTest.Services.Interfaces;

namespace AIAgentTest.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ILLMClientService _llmClientService;
        private readonly IContextManager _contextManager;
        
        // Tool settings
        private bool _enableToolCalls = true;
        private int _toolTimeout = 30;
        
        // Context settings
        private int _maxContextLength = 4096;
        private bool _enableContextSummarization = true;
        private int _summarizationThreshold = 10;
        
        // Response settings
        private int _maxResponseLength = 2048;
        private double _temperature = 0.7;
        private double _topP = 0.9;
        private double _frequencyPenalty = 0.0;
        private double _presencePenalty = 0.0;
        
        // Commands
        public ICommand SaveSettingsCommand { get; }
        public ICommand ResetSettingsCommand { get; }
        
        #region Properties
        
        // Tool settings
        public bool EnableToolCalls
        {
            get => _enableToolCalls;
            set => SetProperty(ref _enableToolCalls, value);
        }
        
        public int ToolTimeout
        {
            get => _toolTimeout;
            set => SetProperty(ref _toolTimeout, value);
        }
        
        // Context settings
        public int MaxContextLength
        {
            get => _maxContextLength;
            set => SetProperty(ref _maxContextLength, value);
        }
        
        public bool EnableContextSummarization
        {
            get => _enableContextSummarization;
            set => SetProperty(ref _enableContextSummarization, value);
        }
        
        public int SummarizationThreshold
        {
            get => _summarizationThreshold;
            set => SetProperty(ref _summarizationThreshold, value);
        }
        
        // Response settings
        public int MaxResponseLength
        {
            get => _maxResponseLength;
            set => SetProperty(ref _maxResponseLength, value);
        }
        
        public double Temperature
        {
            get => _temperature;
            set => SetProperty(ref _temperature, Math.Clamp(value, 0.0, 2.0));
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
        
        #endregion
        
        public SettingsViewModel(ILLMClientService llmClientService, IContextManager contextManager)
        {
            _llmClientService = llmClientService ?? throw new ArgumentNullException(nameof(llmClientService));
            _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
            
            // Initialize commands
            SaveSettingsCommand = new RelayCommand(SaveSettings);
            ResetSettingsCommand = new RelayCommand(ResetSettings);
            
            // Load settings
            LoadSettings();
        }
        
        private void LoadSettings()
        {
            try
            {
                // Load from application settings
                if (Properties.Settings.Default.EnableToolCalls != null)
                    EnableToolCalls = Properties.Settings.Default.EnableToolCalls;
                
                if (Properties.Settings.Default.ToolTimeout > 0)
                    ToolTimeout = Properties.Settings.Default.ToolTimeout;
                
                if (Properties.Settings.Default.MaxContextLength > 0)
                    MaxContextLength = Properties.Settings.Default.MaxContextLength;
                
                // More settings loading as needed
                
                // Apply to services
                ApplySettingsToServices();
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }
        }
        
        private void SaveSettings()
        {
            try
            {
                // Save to application settings
                Properties.Settings.Default.EnableToolCalls = EnableToolCalls;
                Properties.Settings.Default.ToolTimeout = ToolTimeout;
                Properties.Settings.Default.MaxContextLength = MaxContextLength;
                Properties.Settings.Default.EnableContextSummarization = EnableContextSummarization;
                Properties.Settings.Default.SummarizationThreshold = SummarizationThreshold;
                Properties.Settings.Default.MaxResponseLength = MaxResponseLength;
                Properties.Settings.Default.Temperature = Temperature;
                Properties.Settings.Default.TopP = TopP;
                Properties.Settings.Default.FrequencyPenalty = FrequencyPenalty;
                Properties.Settings.Default.PresencePenalty = PresencePenalty;
                
                Properties.Settings.Default.Save();
                
                // Apply to services
                ApplySettingsToServices();
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
        
        private void ResetSettings()
        {
            // Reset to defaults
            EnableToolCalls = true;
            ToolTimeout = 30;
            MaxContextLength = 4096;
            EnableContextSummarization = true;
            SummarizationThreshold = 10;
            MaxResponseLength = 2048;
            Temperature = 0.7;
            TopP = 0.9;
            FrequencyPenalty = 0.0;
            PresencePenalty = 0.0;
            
            // Apply to services
            ApplySettingsToServices();
        }
        
        private void ApplySettingsToServices()
        {
            try
            {
                // Update context manager settings
                if (_contextManager != null)
                {
                    _contextManager.IsContextEnabled = true; // Always enable context
                    
                    // Add more context manager settings when available
                }
                
                // Update LLM service settings
                if (_llmClientService != null)
                {
                    // Update LLM client settings when available
                    if (_llmClientService is ILLMSettingsProvider llmSettings)
                    {
                        llmSettings.SetTemperature(Temperature);
                        llmSettings.SetMaxTokens(MaxResponseLength);
                        
                        // Set other settings when available
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error
                Console.WriteLine($"Error applying settings to services: {ex.Message}");
            }
        }
    }
}