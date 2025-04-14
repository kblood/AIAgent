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
        private bool _isToolManagerVisible = false;
        private bool _isMCPServerManagerVisible = false;
        private bool _isSettingsVisible = false;
        private bool _isFilesystemManagerVisible = false;
        private bool _isToolTestingVisible = false;
        
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
        
        public bool IsToolManagerVisible
        {
            get => _isToolManagerVisible;
            set => SetProperty(ref _isToolManagerVisible, value);
        }
        
        public bool IsMCPServerManagerVisible
        {
            get => _isMCPServerManagerVisible;
            set => SetProperty(ref _isMCPServerManagerVisible, value);
        }
        
        public bool IsSettingsVisible
        {
            get => _isSettingsVisible;
            set => SetProperty(ref _isSettingsVisible, value);
        }
        
        public bool IsFilesystemManagerVisible
        {
            get => _isFilesystemManagerVisible;
            set => SetProperty(ref _isFilesystemManagerVisible, value);
        }
        
        public bool IsToolTestingVisible
        {
            get => _isToolTestingVisible;
            set => SetProperty(ref _isToolTestingVisible, value);
        }
        
        // Child View Models
        public ModelSelectionViewModel ModelVM { get; }
        public CodeViewModel CodeVM { get; }
        public DebugViewModel DebugVM { get; }
        public ChatSessionViewModel ChatVM { get; }
        public ToolManagerViewModel ToolManagerVM { get; set; }
        public MCPServerManagerViewModel MCPServerManagerVM { get; set; }
        public SettingsViewModel SettingsVM { get; set; }
        public FilesystemManagerViewModel FilesystemManagerVM { get; set; }
        public ToolTestingViewModel ToolTestingVM { get; set; }
        
        // Commands
        public ICommand ToggleLightThemeCommand { get; }
        public ICommand ToggleDarkThemeCommand { get; }
        public ICommand ToggleDebugCommand { get; }
        public ICommand ToggleModelSelectionCommand { get; }
        public ICommand ToggleToolManagerCommand { get; }
        public ICommand ToggleMCPServerManagerCommand { get; }
        public ICommand ToggleSettingsCommand { get; }
        public ICommand ToggleFilesystemManagerCommand { get; }
        public ICommand ToggleToolTestingCommand { get; }
        public ICommand ExitCommand { get; }
        public ICommand ShowAboutCommand { get; }
        
        public MainViewModel(
            ThemeService themeService,
            ModelSelectionViewModel modelVM,
            CodeViewModel codeVM,
            DebugViewModel debugVM,
            ChatSessionViewModel chatVM,
            ToolManagerViewModel toolManagerVM = null,
            MCPServerManagerViewModel mcpServerManagerVM = null,
            SettingsViewModel settingsVM = null,
            FilesystemManagerViewModel filesystemManagerVM = null,
            ToolTestingViewModel toolTestingVM = null)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            ModelVM = modelVM ?? throw new ArgumentNullException(nameof(modelVM));
            CodeVM = codeVM ?? throw new ArgumentNullException(nameof(codeVM));
            DebugVM = debugVM ?? throw new ArgumentNullException(nameof(debugVM));
            ChatVM = chatVM ?? throw new ArgumentNullException(nameof(chatVM));
            ToolManagerVM = toolManagerVM;
            MCPServerManagerVM = mcpServerManagerVM;
            SettingsVM = settingsVM;
            FilesystemManagerVM = filesystemManagerVM;
            ToolTestingVM = toolTestingVM;
            
            // Initialize properties
            _isLightTheme = Properties.Settings.Default.IsLightTheme;
            
            // Initialize commands
            ToggleLightThemeCommand = new RelayCommand(() => IsLightTheme = true);
            ToggleDarkThemeCommand = new RelayCommand(() => IsLightTheme = false);
            ToggleDebugCommand = new RelayCommand(() => {
                DebugVM.IsVisible = !DebugVM.IsVisible;
                System.Diagnostics.Debug.WriteLine($"Debug panel visibility set to: {DebugVM.IsVisible}");
                OnPropertyChanged(nameof(DebugVM));
            });
            ToggleModelSelectionCommand = new RelayCommand(() => IsModelSelectionVisible = !IsModelSelectionVisible);
            ToggleToolManagerCommand = new RelayCommand(() => IsToolManagerVisible = !IsToolManagerVisible, () => ToolManagerVM != null);
            ToggleMCPServerManagerCommand = new RelayCommand(() => IsMCPServerManagerVisible = !IsMCPServerManagerVisible, () => MCPServerManagerVM != null);
            ToggleSettingsCommand = new RelayCommand(() => IsSettingsVisible = !IsSettingsVisible, () => SettingsVM != null);
            ToggleFilesystemManagerCommand = new RelayCommand(() => IsFilesystemManagerVisible = !IsFilesystemManagerVisible, () => FilesystemManagerVM != null);
            ToggleToolTestingCommand = new RelayCommand(() => IsToolTestingVisible = !IsToolTestingVisible, () => ToolTestingVM != null);
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