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
    public partial class MainWindow2 : Window, INotifyPropertyChanged
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

        public MainWindow2()
        {
            InitializeComponent();
            DataContext = this;

            _ollamaClient = new OllamaClient();
            AvailableModels = new ObservableCollection<string>();

            LoadModelsAsync();
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
                AppendToConversation("User: " + InputText + "\n");

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
            AppendToConversation("Assistant: ");

            string pattern = @"```(\w*)\s*\n(.*?)\n```|`(\w*)\s*\n(.*?)\n`";
            int lastIndex = 0;

            foreach (Match match in Regex.Matches(response, pattern, RegexOptions.Singleline))
            {
                // Add text before code block
                string textBefore = response.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrWhiteSpace(textBefore))
                {
                    AppendToConversation(textBefore);
                }

                // Get language and code
                string language = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
                string code = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;

                // Show code in code window
                SetRichTextContent(CodeBox, code);

                // Add clickable link to conversation
                AddCodeLink(language, code);

                lastIndex = match.Index + match.Length;
            }

            // Add remaining text
            string remaining = response.Substring(lastIndex);
            if (!string.IsNullOrWhiteSpace(remaining))
            {
                AppendToConversation(remaining);
            }

            AppendToConversation("\n");
        }

        private void AppendToConversation(string text)
        {
            var paragraph = new Paragraph(new Run(text));
            ConversationBox.Document.Blocks.Add(paragraph);
        }

        private void AddCodeLink(string language, string code)
        {
            var paragraph = new Paragraph();
            var link = new Hyperlink(new Run($"[View {language} Code]"))
            {
                Foreground = Brushes.Blue
            };

            link.Click += (s, e) =>
            {
                // Update code with visual feedback
                CodeBox.Background = Brushes.LightYellow;
                SetRichTextContent(CodeBox, code);

                // Reset background after delay
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(500)
                };
                timer.Tick += (s, e) =>
                {
                    CodeBox.Background = Brushes.White;
                    timer.Stop();
                };
                timer.Start();
            };

            paragraph.Inlines.Add(link);
            ConversationBox.Document.Blocks.Add(paragraph);
        }

        private void SetRichTextContent(RichTextBox box, string content)
        {
            var document = new FlowDocument();
            document.Blocks.Add(new Paragraph(new Run(content)));
            box.Document = document;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}