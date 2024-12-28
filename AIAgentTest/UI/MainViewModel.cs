using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using AIAgentTest.API_Clients;
using System.Text.RegularExpressions;
using MessageBox = System.Windows.Forms.MessageBox;
using FontFamily = System.Windows.Media.FontFamily;
using Brushes = System.Windows.Media.Brushes;

namespace AIAgentFramework
{
    public partial class MainWindowViewModel : ObservableObject
    {
        private readonly OllamaClient _ollamaClient;

        [ObservableProperty]
        private ObservableCollection<string> availableModels;

        [ObservableProperty]
        private string selectedModel;

        [ObservableProperty]
        private string inputText = string.Empty;

        [ObservableProperty]
        private FlowDocument conversationDocument;

        [ObservableProperty]
        private TextDocument codeDocument;

        [ObservableProperty]
        private TextDocument debugDocument;

        [ObservableProperty]
        private IHighlightingDefinition currentSyntaxHighlighting;

        public MainWindowViewModel()
        {
            _ollamaClient = new OllamaClient();

            // Initialize documents
            ConversationDocument = new FlowDocument();
            CodeDocument = new TextDocument();
            DebugDocument = new TextDocument();

            // Load models
            LoadModelsAsync();
        }

        private async void LoadModelsAsync()
        {
            try
            {
                var models = await _ollamaClient.GetAvailableModelsAsync();
                AvailableModels = new ObservableCollection<string>(
                    models.Select(m => m.Name)
                );

                if (AvailableModels.Any())
                {
                    SelectedModel = AvailableModels.First();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading models: {ex.Message}");
            }
        }

        public async void SubmitInput()
        {
            if (string.IsNullOrWhiteSpace(InputText) || string.IsNullOrWhiteSpace(SelectedModel))
                return;

            try
            {
                // Add user input to conversation
                AddToConversation("User: " + InputText + "\n", false);

                // Get AI response
                string response = await _ollamaClient.GenerateTextResponseAsync(InputText, SelectedModel);

                // Show raw response in debug
                DebugDocument.Text = response;

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
            AddToConversation("Assistant: ", false);

            // Match code blocks
            string pattern = @"```(\w*)\s*\n(.*?)\n```|`(\w*)\s*\n(.*?)\n`";
            int lastIndex = 0;

            foreach (Match match in Regex.Matches(response, pattern, RegexOptions.Singleline))
            {
                // Add text before code block
                string textBefore = response.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrWhiteSpace(textBefore))
                {
                    AddToConversation(textBefore, false);
                }

                // Get language and code
                string language = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
                string code = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;

                // Display code in code window
                CodeDocument.Text = code;
                UpdateSyntaxHighlighting(language);

                // Add clickable link to conversation
                AddCodeLink(language, code);

                lastIndex = match.Index + match.Length;
            }

            // Add remaining text
            string remaining = response.Substring(lastIndex);
            if (!string.IsNullOrWhiteSpace(remaining))
            {
                AddToConversation(remaining, false);
            }

            AddToConversation("\n", false);
        }

        private void AddToConversation(string text, bool isCode)
        {
            var paragraph = new Paragraph();
            var run = new Run(text);

            if (isCode)
            {
                run.FontFamily = new FontFamily("Consolas");
            }

            paragraph.Inlines.Add(run);
            ConversationDocument.Blocks.Add(paragraph);
        }

        private void AddCodeLink(string language, string code)
        {
            var paragraph = new Paragraph();
            var hyperlink = new Hyperlink(new Run($"[View {language} Code]"))
            {
                Foreground = Brushes.Blue,
                TextDecorations = TextDecorations.Underline
            };

            hyperlink.Click += (s, e) =>
            {
                CodeDocument.Text = code;
                UpdateSyntaxHighlighting(language);
            };

            paragraph.Inlines.Add(hyperlink);
            ConversationDocument.Blocks.Add(paragraph);
        }

        private void UpdateSyntaxHighlighting(string language)
        {
            try
            {
                // Map common language names to AvalonEdit highlighting definitions
                string highlightingName = language.ToLower() switch
                {
                    "csharp" or "cs" => "C#",
                    "python" or "py" => "Python",
                    "javascript" or "js" => "JavaScript",
                    "html" => "HTML",
                    "xml" => "XML",
                    "css" => "CSS",
                    _ => null
                };

                if (highlightingName != null)
                {
                    CurrentSyntaxHighlighting = HighlightingManager.Instance.GetDefinition(highlightingName);
                }
            }
            catch
            {
                // Fallback to no syntax highlighting if definition not found
                CurrentSyntaxHighlighting = null;
            }
        }
    }
}