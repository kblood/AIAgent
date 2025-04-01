using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using AIAgentTest.ViewModels;

namespace AIAgentTest.Views
{
    public partial class CodePanel : UserControl
    {
        public CodePanel()
        {
            InitializeComponent();
            
            // Subscribe to data context changes
            DataContextChanged += CodePanel_DataContextChanged;
        }
        
        private void CodePanel_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is CodeViewModel viewModel)
            {
                // Update the code display when the view model's code changes
                viewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(CodeViewModel.CurrentCode))
                    {
                        UpdateCodeDisplay(viewModel.CurrentCode);
                    }
                };
                
                // Initial update
                UpdateCodeDisplay(viewModel.CurrentCode);
            }
        }
        
        private void UpdateCodeDisplay(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                CodeBox.Document = new FlowDocument();
                return;
            }
            
            var document = new FlowDocument();
            var paragraph = new Paragraph(new Run(code))
            {
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                LineHeight = 1.0
            };
            document.Blocks.Add(paragraph);
            CodeBox.Document = document;
            CodeBox.ScrollToEnd();
        }
    }
}
