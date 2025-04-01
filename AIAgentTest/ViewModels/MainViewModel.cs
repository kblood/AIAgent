using System;
using System.Windows.Input;
using AIAgentTest.Commands;
using AIAgentTest.Services;
using AIAgentTest.Services.Interfaces;

namespace AIAgentTest.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ThemeService _themeService;
        
        private bool _isLightTheme;
        private bool _isModelSelectionVisible = true;
        
        public bool IsLightTheme
        {
            get => _isLightTheme;
            set
            {
                if (SetProperty(ref _isLightTheme, value))
                {
                    OnPropertyChanged(nameof(IsDarkTheme));
                    _themeService.SetTheme(value ? ThemeType.Light : ThemeType.Dark);
                }
            }
        }
        
        public bool IsDarkTheme => !_isLightTheme;
        
        public bool IsModelSelectionVisible
        {
            get => _isModelSelectionVisible;
            set => SetProperty(ref _isModelSelectionVisible, value);
        }
        
        // Child View Models
        public ModelSelectionViewModel ModelVM { get; }
        public CodeViewModel CodeVM { get; }
        public DebugViewModel DebugVM { get; }
        public ChatSessionViewModel ChatVM { get; }
        
        // Commands
        public ICommand ToggleLightThemeCommand { get; }
        public ICommand ToggleDarkThemeCommand { get; }
        public ICommand ToggleDebugCommand { get; }
        public ICommand ToggleModelSelectionCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand ShowAboutCommand { get; }
        
        public MainViewModel(
            ThemeService themeService,
            ModelSelectionViewModel modelVM,
            CodeViewModel codeVM,
            DebugViewModel debugVM,
            ChatSessionViewModel chatVM)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            ModelVM = modelVM ?? throw new ArgumentNullException(nameof(modelVM));
            CodeVM = codeVM ?? throw new ArgumentNullException(nameof(codeVM));
            DebugVM = debugVM ?? throw new ArgumentNullException(nameof(debugVM));
            ChatVM = chatVM ?? throw new ArgumentNullException(nameof(chatVM));
            
            // Initialize properties
            _isLightTheme = Properties.Settings.Default.IsLightTheme;
            
            // Initialize commands
            ToggleLightThemeCommand = new RelayCommand(() => IsLightTheme = true);
            ToggleDarkThemeCommand = new RelayCommand(() => IsLightTheme = false);
            ToggleDebugCommand = new RelayCommand(() => DebugVM.IsVisible = !DebugVM.IsVisible);
            ToggleModelSelectionCommand = new RelayCommand(() => IsModelSelectionVisible = !IsModelSelectionVisible);
            ExitCommand = new RelayCommand(() => System.Windows.Application.Current.Shutdown());
            ShowAboutCommand = new RelayCommand(ShowAbout);
            
            // Set initial theme
            _themeService.SetTheme(IsLightTheme ? ThemeType.Light : ThemeType.Dark);
            
            // Connect events between view models
            ChatVM.CodeExtracted += (s, e) => CodeVM.SetCode(e.Code, e.Language);
        }
        
        private void ShowAbout()
        {
            System.Windows.MessageBox.Show(
                "AI Agent Framework\nVersion 1.0\n\nA user interface for interacting with local AI models.",
                "About", 
                System.Windows.MessageBoxButton.OK, 
                System.Windows.MessageBoxImage.Information);
        }
    }
}