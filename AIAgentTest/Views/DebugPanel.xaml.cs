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
            var document = new FlowDocument();
            var paragraph = new Paragraph(new Run(content ?? string.Empty))
            {
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                LineHeight = 1.0
            };
            document.Blocks.Add(paragraph);
            DebugBox.Document = document;
            DebugBox.ScrollToEnd();
        }
    }
}
