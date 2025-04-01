using System;
using System.Threading.Tasks;
using System.Windows.Input;
using AIAgentTest.Commands;
using AIAgentTest.Services.Interfaces;

namespace AIAgentTest.ViewModels
{
    public class DebugViewModel : ViewModelBase
    {
        private readonly IContextManager _contextManager;
        
        private bool _isVisible;
        private string _debugContent;
        
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
        
        // Commands
        public ICommand ShowContextCommand { get; }
        public ICommand ClearContextCommand { get; }
        public ICommand SummarizeContextCommand { get; }
        
        public DebugViewModel(IContextManager contextManager)
        {
            _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
            
            // Initialize with default values
            _isVisible = true;
            _debugContent = "Debug information will appear here.";
            
            // Initialize commands
            ShowContextCommand = new RelayCommand(ShowContext, () => IsVisible);
            ClearContextCommand = new RelayCommand(ClearContext);
            SummarizeContextCommand = new RelayCommand(async () => await SummarizeContext());
        }
        
        private void ShowContext()
        {
            DebugContent = _contextManager.GetFullContext();
        }
        
        private void ClearContext()
        {
            _contextManager.ClearContext();
            DebugContent = "Context cleared.";
        }
        
        private async Task SummarizeContext()
        {
            try
            {
                DebugContent = "Summarizing context...";
                await _contextManager.SummarizeContext(_contextManager.DefaultModel);
                DebugContent = _contextManager.GetDebugInfo();
            }
            catch (Exception ex)
            {
                DebugContent = $"Error summarizing context: {ex.Message}";
            }
        }
    }
}