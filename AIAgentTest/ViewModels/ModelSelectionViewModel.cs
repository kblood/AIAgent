using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using AIAgentTest.Commands;
using AIAgentTest.Services.Interfaces;

namespace AIAgentTest.ViewModels
{
    public class ModelSelectionViewModel : ViewModelBase
    {
        private readonly ILLMClientService _llmClientService;
        
        private ObservableCollection<string> _availableModels;
        private string _selectedModel;
        
        public ObservableCollection<string> AvailableModels
        {
            get => _availableModels;
            set => SetProperty(ref _availableModels, value);
        }
        
        public string SelectedModel
        {
            get => _selectedModel;
            set => SetProperty(ref _selectedModel, value);
        }
        
        // Commands
        public ICommand RefreshModelsCommand { get; }
        public ICommand ShowModelSettingsCommand { get; }
        
        public ModelSelectionViewModel(ILLMClientService llmClientService)
        {
            _llmClientService = llmClientService ?? throw new ArgumentNullException(nameof(llmClientService));
            
            _availableModels = new ObservableCollection<string>();
            
            // Add default model at startup
            _availableModels.Add("llama3"); // Default model if none available
            _selectedModel = "llama3";
            
            // Initialize commands
            RefreshModelsCommand = new RelayCommand(async () => await LoadModelsAsync());
            ShowModelSettingsCommand = new RelayCommand(ShowModelSettings);
            
            // Subscribe to events
            _llmClientService.ModelsLoaded += OnModelsLoaded;
            
            // Load models
            LoadModelsAsync().ConfigureAwait(false);
        }
        
        private void OnModelsLoaded(object sender, ModelLoadedEventArgs e)
        {
            AvailableModels.Clear();
            foreach (var model in e.Models)
            {
                AvailableModels.Add(model);
            }
            
            if (AvailableModels.Count > 0 && string.IsNullOrEmpty(SelectedModel))
            {
                SelectedModel = AvailableModels[0];
            }
        }
        
        private async Task LoadModelsAsync()
        {
            try
            {
                await _llmClientService.GetAvailableModelsAsync();
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"Error loading models: {ex.Message}");
            }
        }
        
        private void ShowModelSettings()
        {
            // This would typically show a dialog with model settings
            // For now, it's just a placeholder
        }
    }
}