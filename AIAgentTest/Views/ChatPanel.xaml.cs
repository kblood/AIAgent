using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AIAgentTest.ViewModels;
using Microsoft.Win32;

namespace AIAgentTest.Views
{
    public partial class ChatPanel : UserControl
    {
        public ChatPanel()
        {
            InitializeComponent();
            
            // Register with the view model after data context is set
            DataContextChanged += ChatPanel_DataContextChanged;
        }
        
        private void ChatPanel_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is ChatSessionViewModel oldVM)
            {
                // Unregister from old view model
                oldVM.AppendTextAction = null;
                oldVM.HandleCodeAction = null;
                oldVM.AppendImageAction = null;
                oldVM.ClearConversationAction = null;
            }
            
            if (e.NewValue is ChatSessionViewModel newVM)
            {
                // Register with new view model
                newVM.AppendTextAction = AppendText;
                newVM.HandleCodeAction = AddCodeLink;
                newVM.AppendImageAction = AppendImage;
                newVM.ClearConversationAction = ClearConversation;
            }
        }
        
        private void AddImageButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image files (*.png;*.jpg;*.jpeg;*.gif;*.bmp)|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All files (*.*)|*.*",
                Title = "Select an image"
            };
            
            if (dialog.ShowDialog() == true && DataContext is ChatSessionViewModel vm)
            {
                vm.SelectedImagePath = dialog.FileName;
            }
        }
        
        private void InputTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && 
                !System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control) &&
                DataContext is ChatSessionViewModel vm)
            {
                e.Handled = true;
                vm.SendCommand.Execute(null);
            }
        }
        
        private void AppendText(string text)
        {
            var paragraph = ConversationBox.Document.Blocks.LastBlock as Paragraph;
            if (paragraph == null)
            {
                paragraph = new Paragraph();
                ConversationBox.Document.Blocks.Add(paragraph);
            }
            
            paragraph.Inlines.Add(new Run(text));
            ConversationBox.ScrollToEnd();
        }
        
        private void AppendImage(string imagePath)
        {
            try
            {
                string absolutePath = System.IO.Path.GetFullPath(imagePath);
                var paragraph = new Paragraph();
                var image = new System.Windows.Controls.Image
                {
                    Source = new BitmapImage(new Uri(absolutePath)),
                    MaxHeight = 400,
                    MaxWidth = 400,
                    Stretch = Stretch.Uniform
                };
                
                var container = new InlineUIContainer(image);
                paragraph.Inlines.Add(container);
                ConversationBox.Document.Blocks.Add(paragraph);
                ConversationBox.ScrollToEnd();
            }
            catch (Exception ex)
            {
                AppendText($"\n[Error displaying image: {ex.Message}]\n");
            }
        }
        
        private void AddCodeLink(string language, string code)
        {
            var paragraph = new Paragraph();
            var link = new Hyperlink(new Run($"[View {language} Code]"))
            {
                Foreground = Brushes.Blue,
                TextDecorations = TextDecorations.Underline,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            
            link.Tag = code;
            link.Click += CodeLink_Click;
            
            paragraph.Inlines.Add(link);
            ConversationBox.Document.Blocks.Add(paragraph);
            ConversationBox.ScrollToEnd();
        }
        
        private void ClearConversation()
        {
            // Clear the RichTextBox content
            ConversationBox.Document = new FlowDocument();
        }
        
        private void CodeLink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink link && 
                link.Tag is string code && 
                Window.GetWindow(this) is Window window)
            {
                // Find the main view model
                if (window.DataContext is MainViewModel mainVM)
                {
                    // Extract language from the link text if possible
                    string linkText = ((Run)link.Inlines.FirstInline).Text;
                    string language = "text";
                    
                    if (linkText.Contains("[View ") && linkText.Contains(" Code]"))
                    {
                        language = linkText.Replace("[View ", "").Replace(" Code]", "");
                    }
                    
                    // Update the code view model
                    mainVM.CodeVM.SetCode(code, language);
                }
            }
        }
    }
}