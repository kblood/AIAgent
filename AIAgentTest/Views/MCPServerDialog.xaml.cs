using System.Windows;

namespace AIAgentTest.Views
{
    /// <summary>
    /// Interaction logic for MCPServerDialog.xaml
    /// </summary>
    public partial class MCPServerDialog : Window
    {
        public MCPServerDialog()
        {
            InitializeComponent();
        }
        
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}