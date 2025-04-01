using System;
using System.Threading.Tasks;
using AIAgentTest.Services;
using AIAgentTest.ViewModels;

namespace AIAgentTest.Testing
{
    public class MVVMTester
    {
        public static async Task RunTests()
        {
            System.Diagnostics.Debug.WriteLine("Starting MVVM Architecture Tests");
            
            // Test 1: Validate Services
            System.Diagnostics.Debug.WriteLine("Test 1: Validating Services...");
            var servicesValid = TestUtil.ValidateServices();
            System.Diagnostics.Debug.WriteLine($"Services Valid: {servicesValid}");
            
            // Test 2: Validate ViewModel Connections
            System.Diagnostics.Debug.WriteLine("Test 2: Validating ViewModel Connections...");
            var connectionsValid = TestUtil.ValidateViewModelConnections();
            System.Diagnostics.Debug.WriteLine($"ViewModel Connections Valid: {connectionsValid}");
            
            // Test 3: Validate Data Flow
            System.Diagnostics.Debug.WriteLine("Test 3: Validating Data Flow...");
            var dataFlowValid = await TestUtil.ValidateDataFlow();
            System.Diagnostics.Debug.WriteLine($"Data Flow Valid: {dataFlowValid}");
            
            // Overall result
            var overallValid = servicesValid && connectionsValid && dataFlowValid;
            System.Diagnostics.Debug.WriteLine($"MVVM Architecture Tests Result: {(overallValid ? "PASS" : "FAIL")}");
            
            if (overallValid)
            {
                System.Windows.MessageBox.Show(
                    "MVVM Architecture Tests: PASS\n\nAll tests were successful. The application is ready for use.",
                    "Test Results", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "MVVM Architecture Tests: FAIL\n\nOne or more tests failed. Please check the debug output for details.",
                    "Test Results", 
                    System.Windows.MessageBoxButton.OK, 
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }
}