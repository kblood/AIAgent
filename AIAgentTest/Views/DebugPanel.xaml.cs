using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using AIAgentTest.ViewModels;

namespace AIAgentTest.Views
{
    public partial class DebugPanel : UserControl
    {
        public DebugPanel()
        {
            InitializeComponent();
            
            // Subscribe to data context changes
            DataContextChanged += DebugPanel_DataContextChanged;
        }
        
        private void DebugPanel_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is DebugViewModel viewModel)
            {
                // Update the debug display when the view model's content changes
                viewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(DebugViewModel.DebugContent))
                    {
                        UpdateDebugDisplay(viewModel.DebugContent);
                    }
                };
                
                // Initial update
                UpdateDebugDisplay(viewModel.DebugContent);
            }
        }
        
        private void UpdateDebugDisplay(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                content = string.Empty;
            }
            
            var document = new FlowDocument();
            var lines = content.Split('\n');
            var paragraph = new Paragraph();
            paragraph.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            paragraph.LineHeight = 1.0;
            
            // Process each line to apply special formatting to tool sections
            foreach (var line in lines)
            {
                var run = new Run(line + "\n");
                
                // Apply special formatting to tool section headers
                if (line.StartsWith("--- Tool:") || line.StartsWith("[Tool call:"))
                {
                    run.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkBlue);
                    run.FontWeight = System.Windows.FontWeights.Bold;
                }
                // Format tool input/output lines
                else if (line.StartsWith("Input:"))
                {
                    run.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGreen);
                }
                else if (line.StartsWith("Result:"))
                {
                    run.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkRed);
                }
                // Format JSON lines (lines with a lot of braces/brackets)
                else if (line.Contains("{") && line.Contains("}") && 
                        (line.Contains("\":\"") || line.Contains("\": ")))
                {
                    run.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGoldenrod);
                    run.FontStyle = System.Windows.FontStyles.Italic;
                }
                // Highlight messages from roles
                else if (line.StartsWith("user:") || line.StartsWith("User:"))
                {
                    run.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Navy);
                    run.FontWeight = System.Windows.FontWeights.SemiBold;
                }
                else if (line.StartsWith("assistant:") || line.StartsWith("Assistant:") || 
                          line.Contains("llama") || line.Contains("gpt") || line.Contains("claude"))
                {
                    run.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Purple);
                    run.FontWeight = System.Windows.FontWeights.SemiBold;
                }
                else if (line.StartsWith("system:") || line.StartsWith("System:"))
                {
                    run.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Brown);
                    run.FontWeight = System.Windows.FontWeights.SemiBold;
                }
                
                paragraph.Inlines.Add(run);
            }
            
            document.Blocks.Add(paragraph);
            DebugBox.Document = document;
            DebugBox.ScrollToEnd();
        }
    }
}
