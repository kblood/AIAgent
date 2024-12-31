using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Text.RegularExpressions;
using AIAgentTest.API_Clients;
using AIAgentTest.Services;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media.Imaging;
using AIAgentTest.Models;

namespace AIAgentTest.UI
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly ChatSessionService _chatSessionService;
        private ObservableCollection<ChatSession> _chatSessions;
        private ChatSession _currentSession;

        public ObservableCollection<ChatSession> ChatSessions
        {
            get => _chatSessions;
            set
            {
                _chatSessions = value;
                OnPropertyChanged(nameof(ChatSessions));
            }
        }

        public ChatSession CurrentSession
        {
            get => _currentSession;
            set
            {
                _currentSession = value;
                OnPropertyChanged(nameof(CurrentSession));
                LoadSessionContent();
            }
        }
        private readonly OllamaClient _ollamaClient;
        private readonly ContextManager _contextManager;
        private ObservableCollection<string> _availableModels;
        private string _selectedModel;
        private string _inputText;
        private bool _isDebugVisible = true;
        private bool _isContextEnabled = true;

        public event PropertyChangedEventHandler PropertyChanged;

        private string _selectedImagePath;
        public string SelectedImagePath
        {
            get => _selectedImagePath;
            set
            {
                _selectedImagePath = value;
                OnPropertyChanged(nameof(SelectedImagePath));
            }
        }

        private bool HasSelectedImage => !string.IsNullOrEmpty(SelectedImagePath) && File.Exists(SelectedImagePath);

        private void AddImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files (*.*)|*.*",
                Title = "Select an image"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedImagePath = dialog.FileName;
            }
        }

        public bool IsDebugVisible
        {
            get => _isDebugVisible;
            set
            {
                _isDebugVisible = value;
                OnPropertyChanged(nameof(IsDebugVisible));
            }
        }

        public bool IsContextEnabled
        {
            get => _isContextEnabled;
            set
            {
                _isContextEnabled = value;
                _contextManager.IsContextEnabled = value;
                OnPropertyChanged(nameof(IsContextEnabled));
            }
        }

        public ObservableCollection<string> AvailableModels
        {
            get => _availableModels;
            set
            {
                _availableModels = value;
                OnPropertyChanged(nameof(AvailableModels));
            }
        }

        public string SelectedModel
        {
            get => _selectedModel;
            set
            {
                _selectedModel = value;
                OnPropertyChanged(nameof(SelectedModel));
            }
        }

        public string InputText
        {
            get => _inputText;
            set
            {
                _inputText = value;
                OnPropertyChanged(nameof(InputText));
            }
        }

        public MainWindow()
        {
            _chatSessionService = new ChatSessionService();
            ChatSessions = new ObservableCollection<ChatSession>();
            InitializeComponent();
            DataContext = this;

            _ollamaClient = new OllamaClient();
            _contextManager = new ContextManager(_ollamaClient);
            AvailableModels = new ObservableCollection<string>();

            ConversationBox.Document = new FlowDocument()
            {
                PagePadding = new Thickness(0),
                LineHeight = 1
            };

            LoadModelsAsync();
            LoadSessionsAsync();

            CodeBox.TextChanged += (s, e) => CodeBox.ScrollToEnd();
            ConversationBox.TextChanged += (s, e) => ConversationBox.ScrollToEnd();
        }

        private void UpdateDebugColumnWidth()
        {
            if (IsDebugVisible)
            {
                DebugColumn.Width = new GridLength(1, GridUnitType.Star);
                DebugColumnDefinition.Width = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                DebugColumn.Width = new GridLength(0);
                DebugColumnDefinition.Width = new GridLength(0);
            }
        }

        private async Task LoadModelsAsync()
        {
            try
            {
                var models = await _ollamaClient.GetAvailableModelsAsync();
                AvailableModels.Clear();
                foreach (var model in models)
                {
                    AvailableModels.Add(model.Name);
                }

                if (AvailableModels.Count > 0)
                {
                    SelectedModel = AvailableModels[0];
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading models: {ex.Message}");
            }
        }

        private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                e.Handled = true;
                SubmitInput();
            }
        }

        private async Task SubmitInput()
        {
            var input = InputText;
            _contextManager.AddMessage("user", input);
    
            var contextualPrompt = _contextManager.GetContextualPrompt(input);
            var response = await _ollamaClient.GenerateTextResponseAsync(contextualPrompt, SelectedModel);
    
            _contextManager.AddMessage("assistant", response);
    
            if (await _contextManager.ShouldSummarize())
                await _contextManager.SummarizeContext(SelectedModel);
                
            if (CurrentSession != null)
                await _chatSessionService.SaveSessionAsync(CurrentSession);
                
            AppendToConversation("User: " + input + "\n", null);
            ProcessAndDisplayResponse(response);
            
            InputText = string.Empty;
        }

        private void AppendImageToConversation(string imagePath)
        {
            var paragraph = new Paragraph();
            var image = new System.Windows.Controls.Image
            {
                Source = new BitmapImage(new Uri(imagePath)),
                MaxHeight = 200,
                MaxWidth = 200,
                Stretch = Stretch.Uniform
            };

            var container = new InlineUIContainer(image);
            paragraph.Inlines.Add(container);
            ConversationBox.Document.Blocks.Add(paragraph);
            ConversationBox.ScrollToEnd();
        }

        private void ProcessAndDisplayResponse(string response)
        {
            AppendToConversation($"{SelectedModel}: ", null);

            string pattern = @"```(\w*)\r?\n(.*?)\r?\n```|```(\w*)\s*(.*?)```";
            int lastIndex = 0;

            var matches = Regex.Matches(response, pattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                string textBefore = response.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrWhiteSpace(textBefore))
                {
                    AppendToConversation(textBefore, null);
                }

                string language = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
                string code = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;

                SetRichTextContent(CodeBox, code);
                AddCodeLink(language, code.Trim());

                lastIndex = match.Index + match.Length;
            }

            string remaining = response.Substring(lastIndex);
            if (!string.IsNullOrWhiteSpace(remaining))
            {
                AppendToConversation(remaining, null);
            }

            AppendToConversation("\n", null);
        }

        private void AppendToConversation(string text, FontFamily fontFamily = null)
        {
            var paragraph = new Paragraph();
            var run = new Run(text);

            if (fontFamily != null)
            {
                run.FontFamily = fontFamily;
            }

            paragraph.Inlines.Add(run);
            ConversationBox.Document.Blocks.Add(paragraph);
            ConversationBox.ScrollToEnd();
        }

        private void AddCodeLink(string language, string code)
        {
            var paragraph = new Paragraph();
            var link = new Hyperlink(new Run($"[View {language} Code]"))
            {
                Foreground = Brushes.Blue,
                TextDecorations = TextDecorations.Underline,
                Cursor = Cursors.Hand
            };

            link.Tag = code;
            link.Click += CodeLink_Click;
            link.MouseLeftButtonDown += (s, e) => CodeLink_Click(link, new RoutedEventArgs());

            paragraph.Inlines.Add(link);
            ConversationBox.Document.Blocks.Add(paragraph);
            ConversationBox.ScrollToEnd();
        }

        private void CodeLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink link && link.Tag is string code)
            {
                Dispatcher.Invoke(() =>
                {
                    var document = new FlowDocument();
                    var paragraph = new Paragraph(new Run(code))
                    {
                        FontFamily = new FontFamily("Consolas"),
                        LineHeight = 1.0
                    };
                    document.Blocks.Add(paragraph);
                    CodeBox.Document = document;

                    var originalBackground = CodeBox.Background;
                    CodeBox.Background = Brushes.LightYellow;

                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(500)
                    };
                    timer.Tick += (s, args) =>
                    {
                        CodeBox.Background = originalBackground;
                        timer.Stop();
                    };
                    timer.Start();

                    CodeBox.ScrollToEnd();
                });
            }
        }

        private void SetRichTextContent(RichTextBox box, string content)
        {
            box.Document.Blocks.Clear();
            var document = new FlowDocument();
            document.Blocks.Add(new Paragraph(new Run(content)));
            box.Document = document;
            box.ScrollToEnd();
        }

        // Menu Event Handlers
        private void SaveConversation_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt"
            };

            if (dialog.ShowDialog() == true)
            {
                TextRange range = new TextRange(ConversationBox.Document.ContentStart,
                                              ConversationBox.Document.ContentEnd);
                File.WriteAllText(dialog.FileName, range.Text);
            }
        }

        private void ExportCode_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt"
            };

            if (dialog.ShowDialog() == true)
            {
                TextRange range = new TextRange(CodeBox.Document.ContentStart,
                                              CodeBox.Document.ContentEnd);
                File.WriteAllText(dialog.FileName, range.Text);
            }
        }

        private async void LoadSessionsAsync()
        {
            var sessions = await _chatSessionService.ListSessionsAsync();
            ChatSessions.Clear();
            foreach (var session in sessions)
            {
                ChatSessions.Add(session);
            }
        }

        private void LoadSessionContent()
        {
            if (CurrentSession == null) return;
            
            _contextManager.ClearContext();
            ConversationBox.Document.Blocks.Clear();
            
            foreach (var message in CurrentSession.Messages)
            {
                _contextManager.AddMessage(message.Role, message.Content);
                AppendToConversation($"{message.Role}: {message.Content}\n", null);
            }
        }

        private async void NewSession_Click(object sender, RoutedEventArgs e)
        {
            _contextManager.ClearContext();
            var session = new ChatSession
            {
                Name = $"Chat {ChatSessions.Count + 1}",
                ModelName = SelectedModel
            };
            ChatSessions.Add(session);
            CurrentSession = session;
            ConversationBox.Document.Blocks.Clear();
            await _chatSessionService.SaveSessionAsync(session);
        }

        private async void SaveSession_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentSession == null) return;
            await _chatSessionService.SaveSessionAsync(CurrentSession);
        }

        private async void DeleteSession_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentSession == null) return;
            
            var result = MessageBox.Show(
                "Are you sure you want to delete this session?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.Yes)
            {
                await _chatSessionService.DeleteSessionAsync(CurrentSession.Id);
                ChatSessions.Remove(CurrentSession);
                CurrentSession = null;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleDebugWindow_Click(object sender, RoutedEventArgs e)
        {
            IsDebugVisible = !IsDebugVisible;
            UpdateDebugColumnWidth();
        }

        private async void RefreshModels_Click(object sender, RoutedEventArgs e)
        {
            await LoadModelsAsync();
        }

        private void ModelSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Model settings dialog not implemented yet.", "Model Settings",
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("AI Agent Framework\nVersion 1.0\n\nA user interface for interacting with local AI models.",
                          "About", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ToggleContext_Click(object sender, RoutedEventArgs e)
        {
            IsContextEnabled = !IsContextEnabled;
        }

        private async void SummarizeContext_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var originalCursor = Mouse.OverrideCursor;
                Mouse.OverrideCursor = Cursors.Wait;

                AppendToConversation("\n[System: Summarizing context...]\n", null);
                await _contextManager.SummarizeContext(SelectedModel);
                AppendToConversation("[System: Context has been summarized.]\n", null);

                if (IsDebugVisible)
                {
                    SetRichTextContent(DebugBox, _contextManager.GetDebugInfo());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error summarizing context: {ex.Message}");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private void ClearContext_Click(object sender, RoutedEventArgs e)
        {
            _contextManager.ClearContext();
            AppendToConversation("\n[System: Context has been cleared.]\n", null);

            if (IsDebugVisible)
            {
                SetRichTextContent(DebugBox, _contextManager.GetDebugInfo());
            }
        }

        private void ShowCurrentContext_Click(object sender, RoutedEventArgs e)
        {
            if (!IsDebugVisible)
            {
                MessageBox.Show("Please enable the Debug Window to view the context.", "Debug Window Required");
                return;
            }

            var fullContext = _contextManager.GetFullContext();
            SetRichTextContent(DebugBox, fullContext);
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}