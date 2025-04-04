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
    // Add a reference to the CodeViewModel
    public CodeViewModel CodeViewModel { get; set; }
    
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
            
            // Create a better link text for tool calls
            string linkText = "[View Code]";
            
            // If it's a JSON that contains tool usage, make the link text more descriptive
            if (language.ToLower() == "json" && code.Contains("[Using ") && code.Contains(" tool...]"))
            {
                // Try to extract the tool name to create a better link text
                var match = System.Text.RegularExpressions.Regex.Match(code, @"\[Using (.*?) tool\.\.\.\]");
                if (match.Success)
                {
                    string toolName = match.Groups[1].Value;
                    linkText = $"[View {toolName} tool details]"; 
                }
                else
                {
                    linkText = "[View tool details]";
                }
            }
            else
            {
                linkText = $"[View {language} Code]";
            }
            
            var link = new Hyperlink(new Run(linkText))
            {
                Foreground = Brushes.Blue,
                TextDecorations = TextDecorations.Underline,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            
            link.Tag = code;
            link.Click += CodeLink_Click;
            
            // Add debug output to verify this is called
            System.Diagnostics.Debug.WriteLine($"Adding code link for {language} code");
            
            paragraph.Inlines.Add(link);
            ConversationBox.Document.Blocks.Add(paragraph);
            ConversationBox.ScrollToEnd();
        }
        
        private void ClearConversation()
        {
            // Clear the RichTextBox content
            ConversationBox.Document = new FlowDocument();
        }
        
        // New method for the EventSetter in XAML
        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
        // Call our existing method
        System.Diagnostics.Debug.WriteLine("Hyperlink_Click called");
        CodeLink_Click(sender, e);
        }
        
        private void CodeLink_Click(object sender, RoutedEventArgs e)
        {
        System.Diagnostics.Debug.WriteLine("CodeLink_Click called");
        
        if (sender is Hyperlink link && link.Tag is string code)
        {
            // Extract language from the link text if possible
            string linkText = "";
            if (link.Inlines.FirstInline is Run run)
            {
                linkText = run.Text;
            }
            
            string language = "text";
            
            // Check for standard code link format
            if (linkText.Contains("[View ") && linkText.Contains(" Code]"))
            {
                language = linkText.Replace("[View ", "").Replace(" Code]", "");
            }
            // Check for tool details link format
            else if (linkText.Contains("[View ") && linkText.Contains(" tool details]"))
            {
                language = "json"; // Tool interactions are always in JSON format
            }
            
            System.Diagnostics.Debug.WriteLine($"Code link clicked: {language}, code length: {code.Length}");
            
            // First try to use the direct reference to CodeViewModel
            if (CodeViewModel != null)
            {
                System.Diagnostics.Debug.WriteLine("Using direct CodeViewModel reference");
                CodeViewModel.SetCode(code, language);
                return;
            }
                
            // Fallback to find the main view model through the window
            var window = Window.GetWindow(this);
            if (window?.DataContext is MainViewModel mainVM)
            {
                System.Diagnostics.Debug.WriteLine("Using MainViewModel from window");
                mainVM.CodeVM.SetCode(code, language);
            }
            else
            {
                // For debug purposes, show a message if we couldn't find the view model
                System.Diagnostics.Debug.WriteLine("Could not find the MainViewModel or CodeViewModel!");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Link is invalid: " + (sender is Hyperlink ? "Is hyperlink" : "Not hyperlink") + 
                                          ", " + (sender is Hyperlink hyperlink && hyperlink.Tag is string ? "Has string tag" : "No string tag"));
        }
    }
    }
}