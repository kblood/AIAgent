using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using AIAgentTest.Services.MCP;
using AIAgentTest.ViewModels;

namespace AIAgentTest.Services.MCP.Tests
{
    /// <summary>
    /// Utility to show available filesystem tools in the UI
    /// </summary>
    public class FilesystemToolIntegrationTest
    {
        private readonly IToolRegistry _toolRegistry;
        private readonly ChatSessionViewModel _chatViewModel;
        
        public FilesystemToolIntegrationTest(IToolRegistry toolRegistry, ChatSessionViewModel chatViewModel)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _chatViewModel = chatViewModel ?? throw new ArgumentNullException(nameof(chatViewModel));
        }
        
        /// <summary>
        /// Append information about available filesystem tools to the chat
        /// </summary>
        public async Task ShowAvailableFilesystemTools()
        {
            // Get all filesystem tools
            var tools = _toolRegistry.GetAllTools()
                .Where(t => t.Tags != null && t.Tags.Contains("File System"))
                .OrderBy(t => t.Name)
                .ToList();
                
            if (tools.Count == 0)
            {
                await _chatViewModel.AppendTextAsync("No filesystem tools found.");
                return;
            }
            
            // Create a message with all tool information
            var sb = new StringBuilder();
            sb.AppendLine("## Available Filesystem Tools\n");
            sb.AppendLine("The following filesystem tools have been successfully integrated:\n");
            
            foreach (var tool in tools)
            {
                sb.AppendLine($"- **{tool.Name}**: {tool.Description}");
            }
            
            sb.AppendLine("\nThese tools can be used by models that support the Model Context Protocol (MCP).");
            sb.AppendLine("You can use the **Filesystem Manager** to configure which directories are accessible to these tools.");
            
            // Append the message to the chat
            await _chatViewModel.AppendTextAsync(sb.ToString());
        }
        
        /// <summary>
        /// Run filesystem integration test and show result in chat
        /// </summary>
        public static async Task RunTest()
        {
            try
            {
                var chatViewModel = ServiceProvider.GetService<ChatSessionViewModel>();
                var toolRegistry = ServiceProvider.GetService<IToolRegistry>();
                
                if (chatViewModel == null || toolRegistry == null)
                {
                    MessageBox.Show(
                        "Required services not found. Make sure the application is properly initialized.",
                        "Filesystem Integration Test",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
                
                var test = new FilesystemToolIntegrationTest(toolRegistry, chatViewModel);
                await test.ShowAvailableFilesystemTools();
                
                // Show allowed directories
                var commonTools = ServiceProvider.GetService<CommonTools>();
                if (commonTools != null)
                {
                    // Use list_allowed_directories tool
                    var directoryHandler = toolRegistry.GetToolHandler("list_allowed_directories");
                    if (directoryHandler != null)
                    {
                        var result = await directoryHandler(new { });
                        
                        // Format the result
                        var sb = new StringBuilder();
                        sb.AppendLine("\n## Allowed Directories\n");
                        
                        try
                        {
                            var dirObj = result as dynamic;
                            var directories = dirObj.directories as dynamic;
                            
                            if (directories != null)
                            {
                                foreach (var dir in directories)
                                {
                                    bool exists = dir.exists ?? false;
                                    string path = dir.path ?? "(unknown)";
                                    
                                    sb.AppendLine($"- {path} {(exists ? "(exists)" : "(not found)")}");
                                }
                            }
                            else
                            {
                                sb.AppendLine("No directories are currently allowed.");
                            }
                        }
                        catch
                        {
                            sb.AppendLine("Could not retrieve allowed directories.");
                        }
                        
                        // Append to chat
                        await chatViewModel.AppendTextAsync(sb.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error running filesystem integration test: {ex.Message}",
                    "Filesystem Integration Test",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}