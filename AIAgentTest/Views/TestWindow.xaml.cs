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
            
            // Connect the chat panel to the code view model
            // We need to wait for the template to be applied
            Loaded += (s, e) =>
            {
                // Find the ChatPanel and set its CodeViewModel property
                if (FindName("chatPanel") is ChatPanel chatPanel && viewModel?.CodeVM != null)
                {
                    chatPanel.CodeViewModel = viewModel.CodeVM;
                }

                // Check if the debug view model is properly initialized
                if (viewModel?.DebugVM != null)
                {
                    System.Diagnostics.Debug.WriteLine("Debug View Model is initialized");
                    viewModel.DebugVM.IsVisible = true;
                    viewModel.DebugVM.ShowContextCommand.Execute(null);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Debug View Model is NOT initialized properly");
                }
            };
        }
    }
}