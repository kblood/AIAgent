using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIAgentTest.Services;
using AIAgentTest.ViewModels;
using AIAgentTest.Services.Interfaces;
using AIAgentTest.Models;

namespace AIAgentTest.Testing
{
    /// <summary>
    /// Utility class for testing MVVM components and integration
    /// </summary>
    public static class TestUtil
    {
        /// <summary>
        /// Validates all required services and ensures they are properly registered
        /// </summary>
        public static bool ValidateServices()
        {
            try
            {
                var requiredServices = new Type[]
                {
                    typeof(ILLMClientService),
                    typeof(IChatSessionService),
                    typeof(IContextManager),
                    typeof(IMessageParsingService),
                    typeof(ThemeService),
                    typeof(MainViewModel),
                    typeof(ChatSessionViewModel),
                    typeof(CodeViewModel),
                    typeof(DebugViewModel),
                    typeof(ModelSelectionViewModel)
                };

                foreach (var serviceType in requiredServices)
                {
                    var service = ServiceProvider.GetServiceByType(serviceType);
                    if (service == null)
                    {
                        Console.WriteLine($"Service not registered: {serviceType.Name}");
                        return false;
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating services: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Validates the connection between ViewModels and ensures they are properly linked
        /// </summary>
        public static bool ValidateViewModelConnections()
        {
            try
            {
                var mainViewModel = ServiceProvider.GetService<MainViewModel>();
                
                // Check that all child view models are not null
                if (mainViewModel.ChatVM == null ||
                    mainViewModel.CodeVM == null ||
                    mainViewModel.DebugVM == null ||
                    mainViewModel.ModelVM == null)
                {
                    Console.WriteLine("One or more child ViewModels are null in MainViewModel");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating ViewModel connections: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Validates the data flow by simulating a code extraction scenario
        /// </summary>
        public static async Task<bool> ValidateDataFlow()
        {
            try
            {
                var chatViewModel = ServiceProvider.GetService<ChatSessionViewModel>();
                var codeViewModel = ServiceProvider.GetService<CodeViewModel>();
                
                // Create a dummy event to simulate code extraction
                var testCode = "Console.WriteLine(\"Hello, World!\");";
                var testLanguage = "csharp";
                
                // Simulate code extraction
                var messageParser = ServiceProvider.GetService<IMessageParsingService>();
                
                // Create a TaskCompletionSource to wait for the event to propagate
                var tcs = new TaskCompletionSource<bool>();
                
                // Set a timeout for the test
                var timeoutTask = Task.Delay(1000);
                
                // Set up an event handler to check if the code was received
                EventHandler<CodeExtractedEventArgs> handler = (s, e) =>
                {
                    if (e.Code == testCode && e.Language == testLanguage)
                    {
                        tcs.SetResult(true);
                    }
                };
                
                try
                {
                    // Subscribe to the event
                    messageParser.CodeExtracted += handler;
                    
                    // Trigger the event
                    ((MessageParsingService)messageParser).OnCodeExtracted(testCode, testLanguage);
                    
                    // Wait for the event to propagate or timeout
                    if (await Task.WhenAny(tcs.Task, timeoutTask) == timeoutTask)
                    {
                        Console.WriteLine("Timeout waiting for CodeExtracted event to propagate");
                        return false;
                    }
                    
                    // Check if the code view model has the correct code
                    if (codeViewModel.CurrentCode != testCode || codeViewModel.CurrentLanguage != testLanguage)
                    {
                        Console.WriteLine("Code was not properly set in CodeViewModel");
                        return false;
                    }
                    
                    return true;
                }
                finally
                {
                    // Unsubscribe from the event
                    messageParser.CodeExtracted -= handler;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating data flow: {ex.Message}");
                return false;
            }
        }
    }
}