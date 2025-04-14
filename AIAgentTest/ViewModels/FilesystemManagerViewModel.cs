using AIAgentTest.Commands;
using AIAgentTest.Services.MCP;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Input;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel for managing filesystem directories and settings
    /// </summary>
    public class FilesystemManagerViewModel : ViewModelBase
    {
        private readonly CommonTools _commonTools;
        private readonly DebugViewModel _debugViewModel;
        private ObservableCollection<DirectoryInfo> _allowedDirectories;
        private DirectoryInfo _selectedDirectory;
        
        /// <summary>
        /// Collection of allowed directories
        /// </summary>
        public ObservableCollection<DirectoryInfo> AllowedDirectories
        {
            get => _allowedDirectories;
            set => SetProperty(ref _allowedDirectories, value);
        }
        
        /// <summary>
        /// Currently selected directory
        /// </summary>
        public DirectoryInfo SelectedDirectory
        {
            get => _selectedDirectory;
            set => SetProperty(ref _selectedDirectory, value);
        }
        
        // Commands
        public ICommand AddDirectoryCommand { get; }
        public ICommand RemoveDirectoryCommand { get; }
        public ICommand SaveDirectoriesCommand { get; }
        public ICommand RefreshDirectoriesCommand { get; }
        public ICommand ShowToolsInChatCommand { get; }
        public ICommand ShowUserGuideCommand { get; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="commonTools">Common tools service</param>
        /// <param name="debugViewModel">Debug ViewModel for logging</param>
        public FilesystemManagerViewModel(CommonTools commonTools, DebugViewModel debugViewModel = null)
        {
            _commonTools = commonTools ?? throw new ArgumentNullException(nameof(commonTools));
            _debugViewModel = debugViewModel;
            
            // Initialize properties
            _allowedDirectories = new ObservableCollection<DirectoryInfo>();
            
            // Initialize commands
            AddDirectoryCommand = new RelayCommand(() => ExecuteAddDirectory(), () => CanExecuteAddDirectory());
            RemoveDirectoryCommand = new RelayCommand(() => ExecuteRemoveDirectory(), () => CanExecuteRemoveDirectory());
            SaveDirectoriesCommand = new RelayCommand(() => ExecuteSaveDirectories(), () => CanExecuteSaveDirectories());
            RefreshDirectoriesCommand = new RelayCommand(() => ExecuteRefreshDirectories());
            ShowToolsInChatCommand = new RelayCommand(() => ExecuteShowToolsInChat());
            ShowUserGuideCommand = new RelayCommand(() => ExecuteShowUserGuide());
            
            // Load allowed directories
            LoadAllowedDirectories();
        }
        
        /// <summary>
        /// Load allowed directories from settings
        /// </summary>
        private void LoadAllowedDirectories()
        {
            try
            {
                LogDebug("Loading allowed directories...");
                
                _allowedDirectories.Clear();
                
                // Get directories from settings
                string savedDirs = Properties.Settings.Default.AllowedDirectories;
                if (!string.IsNullOrEmpty(savedDirs))
                {
                    string[] dirs = savedDirs.Split(';');
                    foreach (var dir in dirs)
                    {
                        if (Directory.Exists(dir))
                        {
                            _allowedDirectories.Add(new DirectoryInfo(dir));
                            LogDebug($"Loaded directory: {dir}");
                        }
                    }
                }
                
                // Add directories from CommonTools as well
                if (_commonTools != null)
                {
                    // Get base directories (since we can't access the private list)
                    var baseDirectories = GetCommonToolsDirectories();
                    
                    foreach (var dir in baseDirectories)
                    {
                        if (Directory.Exists(dir))
                        {
                            var dirInfo = new DirectoryInfo(dir);
                            if (!_allowedDirectories.Any(d => d.FullName == dirInfo.FullName))
                            {
                                _allowedDirectories.Add(dirInfo);
                                LogDebug($"Added directory from CommonTools: {dir}");
                            }
                        }
                    }
                }
                
                LogDebug($"Loaded {_allowedDirectories.Count} allowed directories.");
            }
            catch (Exception ex)
            {
                LogDebug($"Error loading allowed directories: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get directories from CommonTools
        /// </summary>
        private string[] GetCommonToolsDirectories()
        {
            // These are the default directories that CommonTools adds
            return new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Path.GetTempPath(),
                AppDomain.CurrentDomain.BaseDirectory,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AIAgent")
            };
        }
        
        /// <summary>
        /// Save allowed directories to settings
        /// </summary>
        private void SaveAllowedDirectories()
        {
            try
            {
                LogDebug("Saving allowed directories...");
                
                // Convert collection to string
                string savedDirs = string.Join(";", _allowedDirectories.Select(d => d.FullName));
                
                // Save to settings
                Properties.Settings.Default.AllowedDirectories = savedDirs;
                Properties.Settings.Default.Save();
                
                LogDebug($"Saved {_allowedDirectories.Count} allowed directories.");
            }
            catch (Exception ex)
            {
                LogDebug($"Error saving allowed directories: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Add an allowed directory
        /// </summary>
        private void ExecuteAddDirectory()
        {
            try
            {
                LogDebug("Adding allowed directory...");
                
                // Use WPF for directory selection
                var dialog = new System.Windows.Controls.TextBox
                {
                    Text = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Width = 400
                };
                
                var selectButton = new System.Windows.Controls.Button
                {
                    Content = "Browse...",
                    Margin = new Thickness(5, 0, 0, 0)
                };
                
                selectButton.Click += (s, e) => {
                    // Create the WPF common file dialog - this is the modern folder picker
                    var openFileDlg = new Microsoft.Win32.OpenFileDialog
                    {
                        // We're looking for folders, not files
                        ValidateNames = false,
                        CheckFileExists = false,
                        CheckPathExists = true,
                        // Using a dummy filename that will be ignored
                        FileName = "Select Folder",
                        Title = "Select a folder to allow file access"
                    };
                    
                    bool? result = openFileDlg.ShowDialog();
                    if (result == true)
                    {
                        // Get the folder by removing the dummy filename
                        string folder = System.IO.Path.GetDirectoryName(openFileDlg.FileName);
                        dialog.Text = folder;
                    }
                };
                
                var panel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Vertical,
                    Margin = new Thickness(10)
                };
                
                var headerText = new System.Windows.Controls.TextBlock
                {
                    Text = "Select a folder to allow file access:",
                    Margin = new Thickness(0, 0, 0, 10),
                    FontWeight = System.Windows.FontWeights.Bold
                };
                
                var inputPanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                
                inputPanel.Children.Add(dialog);
                inputPanel.Children.Add(selectButton);
                
                panel.Children.Add(headerText);
                panel.Children.Add(inputPanel);
                
                var okButton = new System.Windows.Controls.Button
                {
                    Content = "OK",
                    Width = 80,
                    Margin = new Thickness(0, 10, 5, 0),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right
                };
                
                var cancelButton = new System.Windows.Controls.Button
                {
                    Content = "Cancel",
                    Width = 80,
                    Margin = new Thickness(5, 10, 0, 0),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right
                };
                
                var buttonPanel = new System.Windows.Controls.StackPanel
                {
                    Orientation = System.Windows.Controls.Orientation.Horizontal,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right
                };
                
                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                
                panel.Children.Add(buttonPanel);
                
                var window = new System.Windows.Window
                {
                    Title = "Add Allowed Directory",
                    Content = panel,
                    Width = 500,
                    Height = 200,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                    ResizeMode = System.Windows.ResizeMode.NoResize,
                    SizeToContent = System.Windows.SizeToContent.WidthAndHeight
                };
                
                bool dialogResult = false;
                
                okButton.Click += (s, e) => {
                    dialogResult = true;
                    window.Close();
                };
                
                cancelButton.Click += (s, e) => {
                    window.Close();
                };
                
                window.ShowDialog();
                
                if (dialogResult && !string.IsNullOrWhiteSpace(dialog.Text))
                {
                    string selectedPath = dialog.Text;
                    LogDebug($"Selected directory: {selectedPath}");
                    
                    // Check if directory already exists in collection
                    if (_allowedDirectories.Any(d => d.FullName == selectedPath))
                    {
                        LogDebug("Directory already exists in allowed directories.");
                        return;
                    }
                    
                    // Add to collection
                    var dirInfo = new DirectoryInfo(selectedPath);
                    _allowedDirectories.Add(dirInfo);
                    
                    // Add to CommonTools
                    _commonTools.AddAllowedDirectory(selectedPath);
                    
                    // Save to settings
                    SaveAllowedDirectories();
                    
                    LogDebug($"Added directory: {selectedPath}");
                }
            }
            catch (Exception ex)
            {
                LogDebug($"Error adding directory: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Remove an allowed directory
        /// </summary>
        private void ExecuteRemoveDirectory()
        {
            try
            {
                if (SelectedDirectory == null)
                {
                    LogDebug("No directory selected to remove.");
                    return;
                }
                
                LogDebug($"Removing directory: {SelectedDirectory.FullName}");
                
                // Check if it's a default directory
                var baseDirectories = GetCommonToolsDirectories();
                if (baseDirectories.Contains(SelectedDirectory.FullName))
                {
                    var result = System.Windows.MessageBox.Show(
                        $"The directory '{SelectedDirectory.FullName}' is a system directory. Are you sure you want to remove it?",
                        "Remove System Directory",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    
                    if (result != MessageBoxResult.Yes)
                    {
                        LogDebug("Removal of system directory cancelled.");
                        return;
                    }
                }
                
                // Remove from collection
                _allowedDirectories.Remove(SelectedDirectory);
                
                // Save to settings
                SaveAllowedDirectories();
                
                // Reset selected directory
                SelectedDirectory = null;
                
                LogDebug("Directory removed successfully.");
            }
            catch (Exception ex)
            {
                LogDebug($"Error removing directory: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save allowed directories
        /// </summary>
        private void ExecuteSaveDirectories()
        {
            SaveAllowedDirectories();
        }
        
        /// <summary>
        /// Refresh allowed directories
        /// </summary>
        private void ExecuteRefreshDirectories()
        {
            LoadAllowedDirectories();
        }
        
        /// <summary>
        /// Show available tools in chat
        /// </summary>
        private void ExecuteShowToolsInChat()
        {
            try
            {
                LogDebug("Showing filesystem tools in chat...");
                
                // Run the test asynchronously
                Task.Run(async () => await AIAgentTest.Services.MCP.Tests.FilesystemToolIntegrationTest.RunTest())
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogDebug($"Error showing tools in chat: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Show the user guide for filesystem tools
        /// </summary>
        private void ExecuteShowUserGuide()
        {
            try
            {
                LogDebug("Showing filesystem tools user guide...");
                
                // Get the user guide path
                string userGuidePath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, 
                    "Resources", 
                    "FilesystemToolsUserGuide.md");
                
                // Check if the file exists
                if (!System.IO.File.Exists(userGuidePath))
                {
                    System.Windows.MessageBox.Show(
                        "The user guide file could not be found.",
                        "User Guide",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }
                
                // Read the user guide content
                string userGuideContent = System.IO.File.ReadAllText(userGuidePath);
                
                // Create a window to display the user guide
                var window = new System.Windows.Window
                {
                    Title = "Filesystem Tools User Guide",
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
                };
                
                // Create a scrollable text box
                var scrollViewer = new System.Windows.Controls.ScrollViewer
                {
                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    Margin = new System.Windows.Thickness(10)
                };
                
                var textBox = new System.Windows.Controls.TextBox
                {
                    Text = userGuideContent,
                    IsReadOnly = true,
                    TextWrapping = System.Windows.TextWrapping.Wrap,
                    FontFamily = new System.Windows.Media.FontFamily("Consolas")
                };
                
                scrollViewer.Content = textBox;
                window.Content = scrollViewer;
                
                // Show the window
                window.Show();
                
                LogDebug("User guide displayed successfully.");
            }
            catch (Exception ex)
            {
                LogDebug($"Error showing user guide: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"An error occurred while trying to display the user guide: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Check if add directory command can execute
        /// </summary>
        private bool CanExecuteAddDirectory()
        {
            return _commonTools != null;
        }
        
        /// <summary>
        /// Check if remove directory command can execute
        /// </summary>
        private bool CanExecuteRemoveDirectory()
        {
            return SelectedDirectory != null;
        }
        
        /// <summary>
        /// Check if save directories command can execute
        /// </summary>
        private bool CanExecuteSaveDirectories()
        {
            return _allowedDirectories != null && _allowedDirectories.Count > 0;
        }
        
        /// <summary>
        /// Log a debug message
        /// </summary>
        private void LogDebug(string message)
        {
            _debugViewModel?.Log($"FilesystemManager: {message}");
        }
    }
}