using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AIAgentTest.API_Clients;
using AIAgentTest.Services;

namespace AIAgentFramework.UI
{
    public partial class MainForm : Form
    {
        private TextBox inputTextBox;
        private RichTextBox outputTextBox;
        private ComboBox modelComboBox;
        private RichTextBox codeTextBox;
        private RichTextBox debugTextBox;

        private OllamaClient ollamaClient;
        private Dictionary<string, string> codeSnippets = new Dictionary<string, string>();

        public MainForm()
        {
            this.Text = "AI Agent Framework UI";
            this.Width = 1500;
            this.Height = 600;

            // Initialize components if you are not using the designer
            if (inputTextBox == null)
            {
                modelComboBox = new ComboBox { Left = 10, Top = 10, Width = 200 };
                inputTextBox = new TextBox { Left = 220, Top = 10, Width = 740, Height = 30 };
                inputTextBox.KeyDown += OnInputTextBoxKeyDown;

                outputTextBox = new RichTextBox { Left = 10, Top = 50, Width = 470, Height = 500, Font = new Font("Arial", 10), ReadOnly = true };
                codeTextBox = new RichTextBox { Left = 490, Top = 50, Width = 470, Height = 500, Font = new Font("Courier New", 10), ReadOnly = true };
                debugTextBox = new RichTextBox { Left = 970, Top = 50, Width = 470, Height = 500, Font = new Font("Courier New", 10), ReadOnly = true };

                this.Controls.Add(modelComboBox);
                this.Controls.Add(inputTextBox);
                this.Controls.Add(outputTextBox);
                this.Controls.Add(codeTextBox);
                this.Controls.Add(debugTextBox);
            }

            // Initialize OllamaClient
            ollamaClient = new OllamaClient();
            outputTextBox.LinkClicked += outputTextBox_LinkClicked;

            LoadModelsAsync();
        }

        private async void LoadModelsAsync()
        {
            try
            {
                var models = await ollamaClient.GetAvailableModelsAsync();
                modelComboBox.Items.Clear();

                foreach (var model in models)
                {
                    modelComboBox.Items.Add(model.Name);
                }

                if (modelComboBox.Items.Count > 0)
                {
                    modelComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading models: {ex.Message}");
            }
        }

        private async void OnSubmit(string inputText)
        {
            if (string.IsNullOrWhiteSpace(inputText))
            {
                MessageBox.Show("Please enter a valid input.");
                return;
            }

            if (modelComboBox.SelectedItem == null)
            {
                MessageBox.Show("Please select a model.");
                return;
            }

            string selectedModel = modelComboBox.SelectedItem.ToString();

            try
            {
                outputTextBox.AppendText($"User: {inputText}\n");
                string response = await ollamaClient.GenerateTextResponseAsync(inputText, selectedModel);

                DisplayResponse(response);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        private void OnInputTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // Submit on Enter, but not when Control is pressed
                if (!e.Control)
                {
                    e.SuppressKeyPress = true;
                    OnSubmit(inputTextBox.Text);
                    inputTextBox.Clear();
                }
            }
        }

        private void DisplayResponse(string response)
        {
            outputTextBox.AppendText("AI: \n");
            debugTextBox.Text = response;

            string pattern = @"`(\w*)\n(.*?)\n`";
            MatchCollection matches = Regex.Matches(response, pattern, RegexOptions.Singleline | RegexOptions.Multiline);

            int lastIndex = 0;
            foreach (Match match in matches)
            {
                outputTextBox.AppendText(response.Substring(lastIndex, match.Index - lastIndex));

                string language = match.Groups[1].Value;
                string code = match.Groups[2].Value;

                string linkText = $"[View {language} Code]";
                codeSnippets[linkText] = code;

                // Create the link manually
                int startIndex = outputTextBox.TextLength;
                outputTextBox.AppendText(linkText);
                int linkLength = linkText.Length;

                outputTextBox.Select(startIndex, linkLength);
                outputTextBox.SelectionColor = Color.Blue;
                outputTextBox.SelectionFont = new Font(outputTextBox.Font, FontStyle.Underline);
                outputTextBox.DeselectAll();

                outputTextBox.AppendText("\n");

                lastIndex = match.Index + match.Length;
            }
            outputTextBox.AppendText(response.Substring(lastIndex));
            outputTextBox.AppendText("\n");
        }

        private void outputTextBox_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            if (codeSnippets.ContainsKey(e.LinkText))
            {
                codeTextBox.Text = codeSnippets[e.LinkText];
            }
        }

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}