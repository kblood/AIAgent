using System.Windows;
using AIAgentTest.Services;
using AIAgentTest.ViewModels;

namespace AIAgentTest.Views
{
    public partial class TestWindow : Window
    {
        public TestWindow()
        {
            InitializeComponent();
            
            // Get the MainViewModel from the service provider
            var viewModel = ServiceProvider.GetService<MainViewModel>();
            
            // Set the data context
            DataContext = viewModel;
        }
    }
}