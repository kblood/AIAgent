/*
This file has been removed from the project and should be deleted.
We are using TestWindow.xaml.cs instead to demonstrate the MVVM architecture.
See TestWindow.xaml.cs for the implementation of the new UI.
*/

using System.Windows;

namespace AIAgentTest.Views
{
    // This class should not be used
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MessageBox.Show("This window should not be used. Please use TestWindow instead.");
            Close();
        }
    }
}