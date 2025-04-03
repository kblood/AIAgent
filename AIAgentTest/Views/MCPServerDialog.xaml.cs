using System.Windows;
using AIAgentTest.ViewModels;

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
            // Validate input
            var viewModel = DataContext as MCPServerEditorViewModel;
            if (viewModel == null)
                return;

            // Basic validation
            if (string.IsNullOrWhiteSpace(viewModel.Name))
            {
                MessageBox.Show("Server name cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(viewModel.Url))
            {
                MessageBox.Show("Server URL cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Set dialog result and close
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Set dialog result and close
            DialogResult = false;
            Close();
        }
    }
}
