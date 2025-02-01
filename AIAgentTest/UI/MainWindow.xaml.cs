using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using AIAgentTest.Properties;
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
using AIAgentTest.Services;
using System.Text.Json;
using System.Text;
//using LLama;

namespace AIAgentTest.UI
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly ChatSessionService _chatSessionService;

        ObservableCollection<ChatSession> _chatSessions;

        public ObservableCollection<ChatSession> ChatSessions
        {
            get => _chatSessions;
            set
            {
                _chatSessions = value;
                OnPropertyChanged(nameof(ChatSessions));
            }
        }

        private ChatSession _currentSession;
        public ChatSession CurrentSession
        {
            get => _currentSession;
            set
            {
                _currentSession = value;
                OnPropertyChanged(nameof(_currentSession));
                LoadSessionContent();
            }
        }
        private readonly OllamaClient _ollamaClient;
        private readonly OpenedAI_VisionClient _openedAI_VisionClient;
        private readonly ContextManager _contextManager;
        private ObservableCollection<string> _availableModels;
        private string _selectedModel;
        private string _inputText;
        private bool _isDebugVisible = true;
        private bool _isContextEnabled = true;
        private bool _isLightTheme = true;
        private readonly FunctionCallingService _functionService;
    
        public bool IsLightTheme
        {
            get => _isLightTheme;
            set
            {
                _isLightTheme = value;
                OnPropertyChanged(nameof(IsLightTheme));
                OnPropertyChanged(nameof(IsDarkTheme));
            }
        }
    
        //public bool IsDarkTheme => !_isLightTheme;

        public bool IsDarkTheme
        {
            get => !_isLightTheme;
            set
            {
                _isLightTheme = !value;
                OnPropertyChanged(nameof(IsLightTheme));
                OnPropertyChanged(nameof(IsDarkTheme));
            }
        }

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
            _chatSessions = new ObservableCollection<ChatSession>();
            InitializeComponent();
            DataContext = this;
            IsLightTheme = Properties.Settings.Default.IsLightTheme;
            ApplyTheme(IsLightTheme);

            _ollamaClient = new OllamaClient();
            _openedAI_VisionClient = new OpenedAI_VisionClient();
            _contextManager = new ContextManager(_ollamaClient);
            AvailableModels = new ObservableCollection<string>();

            ConversationBox.Document = new FlowDocument()
            {
                PagePadding = new Thickness(0),
                LineHeight = 1
            };

            _functionService = new FunctionCallingService();
            RegisterDefaultFunctions();
            
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

                var runningModels = await _ollamaClient.GetRunningModelsAsync();
                if(runningModels != null && runningModels.Models.Count > 0)
                {
                    SelectedModel = runningModels.Models.First().Name;
                }
                else if (AvailableModels.Count > 0)
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
                EnsureSessionExists();
                SubmitInput();
            }
        }

        private void RegisterDefaultFunctions()
        {
            _functionService.RegisterFunction(
                "get_weather",
                "Get the current weather for a location",
                new Dictionary<string, ParameterDefinition>
                {
                    ["location"] = new ParameterDefinition
                    {
                        Type = "string",
                        Description = "The city name",
                        Required = true
                    }
                },
                async (args) =>
                {
                    var location = args["location"].ToString();
                    return $"Weather in {location}: 22°C, Partly Cloudy";
                }
            );
        }

        // These enums help us keep track of whether we're parsing code, text, or a code language line.
        private enum ParserState
        {
            OutsideCodeBlock,
            DetectingLanguage,
            InsideCodeBlock
        }

        private async Task SubmitInput()
        {
            if (string.IsNullOrWhiteSpace(InputText) ||
                string.IsNullOrWhiteSpace(SelectedModel) ||
                CurrentSession == null)
                return;

            try
            {
                string newChatMessage = "";
                if (!ConversationBox.ToString().EndsWith("\n") && !ConversationBox.ToString().EndsWith("\n\r"))
                {
                    newChatMessage += AppendToConversation("\n", null);
                }
                newChatMessage += AppendToConversation($"User: {InputText}\n", null);

                // 1) Add user message to session
                CurrentSession.Messages.Add(new Models.ChatMessage
                {
                    Role = "User",
                    Content = newChatMessage,
                    ImagePath = HasSelectedImage ? SelectedImagePath : null
                });

                string fullResponse = "";
                if (HasSelectedImage)
                {
                    var response = await _openedAI_VisionClient.GenerateResponseWithImageAsync(InputText, SelectedImagePath, SelectedModel);
                    //var response = await _ollamaClient.GenerateResponseWithImageAsync(InputText, SelectedImagePath, SelectedModel);
                    AppendImageToConversation(SelectedImagePath);
                    
                    fullResponse = response;
                    ProcessAndDisplayMessageWithCode(response);
                    if(response.Contains("<point"))
                    {
                        var molmoImage = ImageService.GetImageWithMolmoPoints(SelectedImagePath, response);
                        AppendImageToConversation(molmoImage);
                        SelectedImagePath = molmoImage;
                    }
                    //SelectedImagePath = null;
                }
                else
                {
                    var functionDefinitions = _functionService.GetFunctionDefinitions();
                    var prompt = _isContextEnabled
                        ? _contextManager.GetContextualPrompt(InputText)
                        : InputText;

                    AppendToConversation($"{SelectedModel}: ", null);

                    // 2) Here is the improved streaming logic for code blocks + language
                    var responseBuilder = new StringBuilder();       // Full textual record
                    var currentCodeBlock = new StringBuilder();      // Accumulate code characters
                    var currentCodeLanguage = new StringBuilder();   // Capture language (if any)

                    bool inCodeBlock = false;          // True once we've found an opening ```
                    int backtickCount = 0;             // Track consecutive backticks
                    ParserState parserState = ParserState.OutsideCodeBlock;

                    // We'll stream the response chunk-by-chunk
                    await foreach (var chunk in _ollamaClient.GenerateStreamResponseAsync(prompt, SelectedModel))
                    {
                        // Update the UI inside the dispatcher
                        await Dispatcher.InvokeAsync(() =>
                        {
                            foreach (char c in chunk)
                            {
                                // Always append to the overall response
                                responseBuilder.Append(c);

                                if (c == '`')
                                {
                                    backtickCount++;

                                    // Check if we hit triple backticks
                                    if (backtickCount == 3)
                                    {
                                        backtickCount = 0; // Reset

                                        if (!inCodeBlock)
                                        {
                                            // We are opening a code block
                                            inCodeBlock = true;
                                            parserState = ParserState.DetectingLanguage;
                                            // Clear old data
                                            currentCodeLanguage.Clear();
                                            currentCodeBlock.Clear();
                                        }
                                        else
                                        {
                                            // We are closing a code block
                                            inCodeBlock = false;
                                            parserState = ParserState.OutsideCodeBlock;

                                            // We have a complete code block in currentCodeBlock
                                            // plus an optional language in currentCodeLanguage.

                                            // Build a well-formed triple-backtick block:
                                            // e.g.:  ```csharp
                                            //         ...code...
                                            //         ```
                                            var codeBlockText = new StringBuilder();
                                            codeBlockText.Append("```");
                                            if (currentCodeLanguage.Length > 0)
                                            {
                                                codeBlockText.Append(currentCodeLanguage);
                                            }
                                            codeBlockText.AppendLine(); // newline after ```lang
                                            codeBlockText.Append(currentCodeBlock);
                                            codeBlockText.AppendLine();
                                            codeBlockText.Append("```");

                                            // Now your existing regex method can parse it:
                                            ProcessAndDisplayMessageWithCode(codeBlockText.ToString());

                                            // Clear for next possible code block
                                            currentCodeBlock.Clear();
                                            currentCodeLanguage.Clear();
                                        }
                                    }
                                    // If we have 1 or 2 backticks, do nothing *yet*.
                                }
                                else
                                {
                                    // c is not a backtick
                                    // If we had partial backticks (1 or 2), flush them appropriately
                                    if (backtickCount > 0)
                                    {
                                        // We are either inside or outside a code block
                                        //  -> If outside, we flush them to conversation
                                        //  -> If inside, flush them to the code builder
                                        if (!inCodeBlock)
                                        {
                                            // We were outside a code block
                                            for (int i = 0; i < backtickCount; i++)
                                                AppendToConversation("`", null);
                                        }
                                        else
                                        {
                                            // We were inside a code block
                                            for (int i = 0; i < backtickCount; i++)
                                                currentCodeBlock.Append('`');

                                            // Refresh partial code in CodeBox
                                            SetRichTextContent(CodeBox, currentCodeBlock.ToString());
                                        }
                                        backtickCount = 0;
                                    }

                                    // Now handle the current char
                                    switch (parserState)
                                    {
                                        case ParserState.OutsideCodeBlock:
                                            // Normal text
                                            AppendToConversation(c.ToString(), null);
                                            break;

                                        case ParserState.DetectingLanguage:
                                            // Immediately after opening ``` - we might capture a language name
                                            // Typically we read until we hit whitespace, newline, or another backtick.
                                            // If c is whitespace/newline, it means the language spec ended.
                                            if (char.IsWhiteSpace(c) || c == '\r' || c == '\n')
                                            {
                                                // Done capturing language, now we are truly "inside" the code
                                                parserState = ParserState.InsideCodeBlock;
                                            }
                                            else
                                            {
                                                // Keep capturing language characters
                                                currentCodeLanguage.Append(c);
                                            }
                                            break;

                                        case ParserState.InsideCodeBlock:
                                            // Accumulate code
                                            currentCodeBlock.Append(c);
                                            // Optionally update the code in real time
                                            SetRichTextContent(CodeBox, currentCodeBlock.ToString());
                                            break;
                                    }
                                }
                            }
                        });

                        await Task.Delay(1); // Small delay to let UI breathe
                    }

                    // 3) If we never closed a code block by the end of the stream,
                    //    we can finalize it anyway (to avoid losing partial code).
                    //    But this is optional. If you prefer to ignore incomplete code blocks, skip this.
                    if (inCodeBlock)
                    {
                        var codeBlockText = new StringBuilder("```");
                        if (currentCodeLanguage.Length > 0)
                        {
                            codeBlockText.Append(currentCodeLanguage);
                        }
                        codeBlockText.AppendLine();
                        codeBlockText.Append(currentCodeBlock);
                        codeBlockText.AppendLine();
                        codeBlockText.Append("```");

                        ProcessAndDisplayMessageWithCode(codeBlockText.ToString());
                    }

                    // 4) Final full response
                    fullResponse = responseBuilder.ToString();
                }

                // Add assistant message to session
                CurrentSession.Messages.Add(new Models.ChatMessage
                {
                    Role = SelectedModel,
                    Content = fullResponse,
                    ImagePath = HasSelectedImage ? SelectedImagePath : null
                });

                // Optionally set debug output
                SetRichTextContent(DebugBox, fullResponse);

                // Possibly rename the session if certain conditions are met
                if (CurrentSession.Messages.Count >= 6 &&
                    CurrentSession.Name.Length < 10 &&
                    (CurrentSession.Name.StartsWith("Chat ") || CurrentSession.Name.StartsWith("New ")))
                {
                    await GenerateSessionNameAsync();
                }

                await _chatSessionService.SaveSessionAsync(CurrentSession);
                InputText = string.Empty;

                // Update context with new messages
                var latestContext = _contextManager.GetLastMessageTimestamp();
                foreach (var msg in CurrentSession.Messages.Where(m => m.Timestamp > latestContext).ToArray())
                {
                    _contextManager.AddMessage(msg.Role, msg.Content);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        private async Task GenerateSessionNameAsync()
        {
            try
            {
                // Create a prompt for the model to generate a session name
                var namePrompt = "Based on this conversation, generate a concise and descriptive name (maximum 30 characters) that captures the main topic or purpose. Respond with only the name, no explanations or quotes:\n\n";

                // Add the conversation content to the prompt
                foreach (var message in CurrentSession.Messages)
                {
                    namePrompt += $"{message.Role}: {message.Content}\n";
                }

                // Get the generated name from the model
                var generatedName = await _ollamaClient.GenerateTextResponseAsync(namePrompt, SelectedModel);

                // Clean up and truncate the name
                generatedName = generatedName.Trim().TrimEnd('.').TrimEnd();
                if (generatedName.Length > 30)
                {
                    generatedName = generatedName.Substring(0, 27) + "...";
                }

                // Update the session name
                CurrentSession.Name = generatedName;
                OnPropertyChanged(nameof(CurrentSession));

                // Save the session with the new name
                await _chatSessionService.SaveSessionAsync(CurrentSession);
            }
            catch (Exception ex)
            {
                // Log the error but don't disrupt the chat flow
                Console.WriteLine($"Error generating session name: {ex.Message}");
            }
        }

        private void AppendImageToConversation(string imagePath)
        {
            string absolutePath = Path.GetFullPath(imagePath);
            var paragraph = new Paragraph();
            var image = new System.Windows.Controls.Image
            {
                Source = new BitmapImage(new Uri(absolutePath)),
                MaxHeight = 400,
                MaxWidth = 400,
                Stretch = Stretch.Uniform
            };

            //var image = new System.Windows.Controls.Image
            //{
            //    Source = new BitmapImage(new Uri(imagePath)),
            //    MaxHeight = 200,
            //    MaxWidth = 200,
            //    Stretch = Stretch.Uniform
            //};

            var container = new InlineUIContainer(image);
            paragraph.Inlines.Add(container);
            ConversationBox.Document.Blocks.Add(paragraph);
            ConversationBox.ScrollToEnd();
        }

        private void ProcessAndDisplayMessageWithCode(string message)
        {
            string pattern = @"```(\w*)\r?\n(.*?)\r?\n```|```(\w*)\s*(.*?)```";
            int lastIndex = 0;

            var matches = Regex.Matches(message, pattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                string textBefore = message.Substring(lastIndex, match.Index - lastIndex);
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

            string remaining = message.Substring(lastIndex);
            if (!string.IsNullOrWhiteSpace(remaining))
            {
                AppendToConversation(remaining, null);
            }

            AppendToConversation("\n", null);
        }

        private string AppendToConversation(string text, FontFamily fontFamily = null)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";

            var paragraph = ConversationBox.Document.Blocks.LastBlock as Paragraph;
            if (paragraph == null)
            {
                paragraph = new Paragraph();
                ConversationBox.Document.Blocks.Add(paragraph);
            }

            var run = new Run(text);
            if (fontFamily != null)
            {
                run.FontFamily = fontFamily;
            }

            paragraph.Inlines.Add(run);
            ConversationBox.ScrollToEnd();
            return text;
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
                    // Use a theme-appropriate highlight color
                    var highlightColor = IsLightTheme ?
                        new SolidColorBrush(Color.FromRgb(255, 255, 224)) : // Light yellow
                        new SolidColorBrush(Color.FromRgb(51, 51, 0));      // Dark yellow

                    CodeBox.Background = highlightColor;

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

        private async Task LoadSessionsAsync()
        {
            var sessions = await _chatSessionService.LoadSessionsAsync();
            ChatSessions.Clear();
            foreach (var session in sessions)
            {
                ChatSessions.Add(session);
            }
        }

        private void LoadSessionContent()
        {
            if (CurrentSession == null) return;

            ConversationBox.Document.Blocks.Clear();
            CodeBox.Document.Blocks.Clear();
            _contextManager.ClearContext();

            foreach (var message in CurrentSession.Messages)
            {
                // Display the message text with code formatting
                AppendToConversation($"{message.Role}: ", null);
                //ProcessAndDisplayMessageWithCode($"{message.Role}: {message.Content}");
                ProcessAndDisplayMessageWithCode($"{message.Content}");

                // If the message has an associated image, display it
                if (!string.IsNullOrEmpty(message.ImagePath) && File.Exists(message.ImagePath))
                {
                    AppendImageToConversation(message.ImagePath);
                }
                _contextManager.AddMessage(message.Role, message.Content);
            }
        }

        private async void EnsureSessionExists()
        {
            if (CurrentSession == null)
            {
                var session = new ChatSession
                {
                    Name = $"Chat {ChatSessions.Count + 1}",
                    ModelName = SelectedModel
                };
                ChatSessions.Add(session);
                CurrentSession = session;
                await _chatSessionService.SaveSessionAsync(session);
            }
        }

        private void SessionNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SaveSessionName();
            }
        }

        private void SessionNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveSessionName();
        }

        private async void SaveSessionName()
        {
            if (CurrentSession != null)
            {
                await _chatSessionService.SaveSessionAsync(CurrentSession);
                OnPropertyChanged(nameof(CurrentSession));
            }
        }

        private async Task CreateNewSession(string name)
        {
            var session = new ChatSession
            {
                Name = name,
                ModelName = SelectedModel
            };
            ChatSessions.Add(session);
            CurrentSession = session;
            await _chatSessionService.SaveSessionAsync(session);
        }

        private async void NewSession_Click(object sender, RoutedEventArgs e)
        {
            var session = new ChatSession
            {
                Name = $"Chat {ChatSessions.Count + 1}",
                ModelName = SelectedModel
            };
            ChatSessions.Add(session);
            CurrentSession = session;  // This will trigger the selection in the ComboBox
            SessionsComboBox.SelectedItem = session;  // Explicitly set the ComboBox selection
            await _chatSessionService.SaveSessionAsync(session);
            _contextManager.ClearContext();
            foreach(var msg in CurrentSession.Messages)
            {
                _contextManager.AddMessage(msg.Role, msg.Content);
            }
        }

        private async void SaveSession_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentSession != null)
            {
                await _chatSessionService.SaveSessionAsync(CurrentSession);
            }
        }

        private async void DeleteSession_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentSession != null)
            {
                await _chatSessionService.DeleteSessionAsync(CurrentSession);
                ChatSessions.Remove(CurrentSession);
                if (ChatSessions.Count == 0)
                {
                    await CreateNewSession("New Chat");
                }
                else
                {
                    CurrentSession = ChatSessions[0];
                }
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
            //IsContextEnabled = !IsContextEnabled;
            if(IsContextEnabled)
            {
                _contextManager.ClearContext();
                foreach (var msg in CurrentSession.Messages)
                {
                    _contextManager.AddMessage(msg.Role, msg.Content);
                }
            }
            else
            {
                _contextManager.ClearContext();
            }
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

        private void ApplyTheme(bool isLight)
        {
            //var theme = isLight ? null : Resources["DarkTheme"] as ResourceDictionary;
            var theme = isLight ?
        Resources["LightTheme"] as ResourceDictionary :
        Resources["DarkTheme"] as ResourceDictionary;
            if (theme != null)
            {
                var keys = new[]
                {
                    "WindowBackground",
                    "TextColor",
                    "MenuBackground",
                    "BorderColor",
                    "ControlBackground",
                    "SelectionBackground",
                    "HyperlinkColor",
                    "MenuItemSelectedBackground",
                    "ComboBoxBackground"
                };
                foreach (var key in keys)
                {
                    if (theme.Contains(key))
                    {
                        Resources[key] = theme[key];
                    }
                }
            }
            else
            {
                // Light theme colors
                Resources["WindowBackground"] = new SolidColorBrush(Colors.White);
                Resources["TextColor"] = new SolidColorBrush(Colors.Black);
                Resources["MenuBackground"] = new SolidColorBrush(Colors.WhiteSmoke);
                Resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(204, 204, 204));
                Resources["ControlBackground"] = new SolidColorBrush(Colors.White);
                Resources["SelectionBackground"] = new SolidColorBrush(Color.FromRgb(173, 216, 230)); // Light blue
                Resources["HyperlinkColor"] = new SolidColorBrush(Color.FromRgb(0, 102, 204));
            }

            Properties.Settings.Default.IsLightTheme = isLight;
            Properties.Settings.Default.Save();
        }

        private void UpdateCodeLinkColors()
        {
            var hyperlinkColor = Resources["HyperlinkColor"] as SolidColorBrush;
            var document = ConversationBox.Document;
            var textRange = new TextRange(document.ContentStart, document.ContentEnd);

            foreach (var block in document.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    foreach (var inline in paragraph.Inlines)
                    {
                        if (inline is Hyperlink hyperlink)
                        {
                            hyperlink.Foreground = hyperlinkColor;
                        }
                    }
                }
            }
        }

        private void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem)
            {
                bool isLight = menuItem.Header.ToString() == "Light";
                IsLightTheme = isLight;
                ApplyTheme(isLight);
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}