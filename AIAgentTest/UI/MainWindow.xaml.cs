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

namespace AIAgentTest.UI
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly OllamaClient _ollamaClient;
        private ObservableCollection<string> _availableModels;
        private string _selectedModel;
        private string _inputText;

        public event PropertyChangedEventHandler PropertyChanged;

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
            InitializeComponent();
            DataContext = this;

            _ollamaClient = new OllamaClient();
            AvailableModels = new ObservableCollection<string>();

            // Initialize FlowDocument with minimal spacing
            ConversationBox.Document = new FlowDocument()
            {
                PagePadding = new Thickness(0),
                LineHeight = 1
            };

            LoadModelsAsync();

            // Ensure the CodeBox and ConversationBox are scrolled to the bottom when content changes
            CodeBox.TextChanged += (s, e) => CodeBox.ScrollToEnd();
            ConversationBox.TextChanged += (s, e) => ConversationBox.ScrollToEnd();
        }

        private async void LoadModelsAsync()
        {
            try
            {
                var models = await _ollamaClient.GetAvailableModelsAsync();
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

        private async void SubmitInput()
        {
            if (string.IsNullOrWhiteSpace(InputText) || string.IsNullOrWhiteSpace(SelectedModel))
                return;

            try
            {
                // Add user input to conversation
                AppendToConversation("User: " + InputText + "\n", null);

                // Get AI response
                string response = await _ollamaClient.GenerateTextResponseAsync(InputText, SelectedModel);

                // Show raw response in debug
                SetRichTextContent(DebugBox, response);

                // Process and display the response
                ProcessAndDisplayResponse(response);

                // Clear input
                InputText = string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        private void ProcessAndDisplayResponse(string response)
        {
            AppendToConversation("Assistant: ", null);

            string pattern = @"```(\w*)\r?\n(.*?)\r?\n```|```(\w*)\s*(.*?)```";
            int lastIndex = 0;

            var matches = Regex.Matches(response, pattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                // Add text before code block
                string textBefore = response.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrWhiteSpace(textBefore))
                {
                    AppendToConversation(textBefore, null);
                }

                // Get language and code
                string language = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
                string code = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;

                // Store the code in CodeBox immediately
                SetRichTextContent(CodeBox, code);

                // Add clickable link to conversation
                AddCodeLink(language, code.Trim());

                lastIndex = match.Index + match.Length;
            }

            // Add remaining text
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
            AppendToDebug($"Creating code link for {language} code");

            try
            {
                // Create the link paragraph
                var paragraph = new Paragraph();
                var link = new Hyperlink(new Run($"[View {language} Code]"))
                {
                    Foreground = Brushes.Blue,
                    TextDecorations = TextDecorations.Underline,
                    Cursor = Cursors.Hand
                };

                // Store the code as a Tag on the link for easy access
                link.Tag = code;
                AppendToDebug($"Code stored in link.Tag, length: {code.Length}");

                // Handle the click event
                link.Click += CodeLink_Click;

                // Add a MouseLeftButtonDown handler as backup
                link.MouseLeftButtonDown += (s, e) =>
                {
                    AppendToDebug("Link clicked via MouseLeftButtonDown");
                    CodeLink_Click(link, new RoutedEventArgs());
                };

                paragraph.Inlines.Add(link);
                ConversationBox.Document.Blocks.Add(paragraph);
                ConversationBox.ScrollToEnd();

                AppendToDebug("Code link created successfully");
            }
            catch (Exception ex)
            {
                AppendToDebug($"Error creating code link: {ex.Message}");
            }
        }

        private void CodeLink_Click(object sender, RoutedEventArgs e)
        {
            // Debug output to DebugBox
            AppendToDebug("CodeLink_Click triggered");

            if (sender is Hyperlink link)
            {
                AppendToDebug($"Link found: {link.Tag != null}");

                if (link.Tag is string code)
                {
                    AppendToDebug($"Code content length: {code.Length}");
                    AppendToDebug("Code content preview: " + code.Substring(0, Math.Min(50, code.Length)));

                    try
                    {
                        Dispatcher.Invoke(() =>
                        {
                            AppendToDebug("Creating new FlowDocument");
                            var document = new FlowDocument();

                            AppendToDebug("Creating code paragraph");
                            var paragraph = new Paragraph(new Run(code))
                            {
                                FontFamily = new FontFamily("Consolas"),
                                LineHeight = 1.0
                            };
                            document.Blocks.Add(paragraph);

                            AppendToDebug("Updating CodeBox document");
                            CodeBox.Document = document;

                            AppendToDebug("Setting background color");
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
                            AppendToDebug("CodeBox update complete");
                        });
                    }
                    catch (Exception ex)
                    {
                        AppendToDebug($"Error updating code window: {ex.Message}");
                    }
                }
                else
                {
                    AppendToDebug("Link.Tag was not a string");
                }
            }
            else
            {
                AppendToDebug("Sender was not a Hyperlink");
            }
        }

        private void AppendToDebug(string message)
        {
            Dispatcher.Invoke(() =>
            {
                var document = new FlowDocument();
                var paragraph = new Paragraph(new Run(message));
                document.Blocks.Add(paragraph);
                DebugBox.Document = document;
                DebugBox.ScrollToEnd();
            });
        }

        private void SetRichTextContent(RichTextBox box, string content)
        {
            box.Document.Blocks.Clear();
            var document = new FlowDocument();
            document.Blocks.Add(new Paragraph(new Run(content)));
            box.Document = document;
            box.ScrollToEnd();
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}