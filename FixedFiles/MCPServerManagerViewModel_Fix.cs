using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using AIAgentTest.Commands;
using System.Collections.Generic;
using AIAgentTest.API_Clients.MCP;
using AIAgentTest.Services;
using AIAgentTest.Services.Interfaces;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AIAgentTest.Views;
using System.Diagnostics;

namespace AIAgentTest.ViewModels
{
    /// <summary>
    /// ViewModel for managing MCP servers
    /// </summary>
    public partial class MCPServerManagerViewModel : ViewModelBase
    {
        // Modified AddServer method to fix --stdio issue and properly handle settings
        private void AddServer()
        {
            try
            {
                Debug.WriteLine("AddServer method called");
                
                // Create a new server editor ViewModel
                var serverEditorViewModel = new MCPServerEditorViewModel
                {
                    IsEditMode = false,
                    ServerName = "",
                    Command = "npx",
                    Arguments = "-y @modelcontextprotocol/server-filesystem C:/" // No --stdio here
                };
                
                // Show the server editor dialog
                var dialog = new MCPServerDialog
                {
                    DataContext = serverEditorViewModel
                };
                
                var result = dialog.ShowDialog();
                if (result.HasValue && result.Value)
                {
                    // Register the new server
                    var serverName = serverEditorViewModel.ServerName;
                    var command = serverEditorViewModel.Command;
                    var argsString = serverEditorViewModel.Arguments.Replace("\\", "/");
                    
                    Debug.WriteLine($"Server dialog returned with name: {serverName}, command: {command}, args: {argsString}");
                    
                    // Parse arguments and ensure --stdio is in the right place
                    var args = argsString.Split(' ');
                    
                    // Check if --stdio is already in the arguments to avoid duplication
                    bool hasStdioFlag = args.Contains("--stdio");
                    List<string> argsList = new List<string>(args);
                    
                    if (!hasStdioFlag)
                    {
                        Debug.WriteLine("--stdio flag not found, adding it...");
                        
                        // Find the right position to insert the --stdio flag (after the package name)
                        int packageIndex = -1;
                        for (int i = 0; i < argsList.Count; i++)
                        {
                            if (argsList[i].Contains("@modelcontextprotocol"))
                            {
                                packageIndex = i;
                                break;
                            }
                        }
                        
                        if (packageIndex >= 0)
                        {
                            // Insert after the package name
                            argsList.Insert(packageIndex + 1, "--stdio");
                            Debug.WriteLine($"Added --stdio after package name at index {packageIndex + 1}");
                        }
                        else
                        {
                            // If package name not found, insert after -y if it exists
                            int yIndex = argsList.IndexOf("-y");
                            if (yIndex >= 0)
                            {
                                argsList.Insert(yIndex + 1, "--stdio");
                                Debug.WriteLine($"Added --stdio after -y at index {yIndex + 1}");
                            }
                            else
                            {
                                // Last resort: insert at the beginning
                                argsList.Insert(0, "--stdio");
                                Debug.WriteLine("Added --stdio at the beginning");
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("--stdio flag already present");
                    }
                    
                    Debug.WriteLine($"Final arguments: {string.Join(" ", argsList)}");
                    
                    // Create a new server client - use StdioMCPServerClient for better reliability
                    var serverClient = new StdioMCPServerClient(command, argsList.ToArray(), Path.GetTempPath(), ServiceProvider.GetService<IDebugLogger>());
                    
                    // Register with the client factory
                    _mcpClientFactory.RegisterMCPServer(serverName, serverClient);
                    
                    // Add to the UI
                    var serverViewModel = new MCPServerViewModel
                    {
                        Name = serverName,
                        Command = command,
                        Args = argsList.ToArray(),
                        ServerClient = serverClient,
                        IsRunning = false
                    };
                    
                    Servers.Add(serverViewModel);
                    
                    // Save the MCP server configuration
                    Debug.WriteLine($"Added new server: {serverName}");
                    
                    // Start the server automatically
                    StartServer(serverViewModel);
                    
                    // Save servers to configuration (this would typically be in a service)
                    SaveServersToConfiguration();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding server: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error adding server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // Modified EditServer method to fix --stdio issue
        private void EditServer()
        {
            if (SelectedServer == null) return;
            
            try
            {
                Debug.WriteLine($"EditServer method called for server: {SelectedServer.Name}");
                
                // Get current args
                var currentArgs = SelectedServer.Args;
                string currentArgsString = "";
                
                if (currentArgs != null)
                {
                    // Filter out --stdio to avoid duplication in UI
                    currentArgsString = string.Join(" ", currentArgs.Where(a => a != "--stdio"));
                    Debug.WriteLine($"Current args (without --stdio): {currentArgsString}");
                }
                else
                {
                    currentArgsString = "-y @modelcontextprotocol/server-filesystem C:/";
                    Debug.WriteLine("Using default args");
                }
                
                // Create a server editor ViewModel with existing values
                var serverEditorViewModel = new MCPServerEditorViewModel
                {
                    IsEditMode = true,
                    ServerName = SelectedServer.Name,
                    Command = "npx", 
                    Arguments = currentArgsString
                };
                
                // Show the server editor dialog
                var dialog = new MCPServerDialog
                {
                    DataContext = serverEditorViewModel
                };
                
                var result = dialog.ShowDialog();
                if (result.HasValue && result.Value)
                {
                    // Remove the old server if the name changed
                    if (SelectedServer.Name != serverEditorViewModel.ServerName)
                    {
                        RemoveServer(false);
                    }
                    
                    // Register the updated server
                    var serverName = serverEditorViewModel.ServerName;
                    var command = serverEditorViewModel.Command;
                    var argsString = serverEditorViewModel.Arguments.Replace("\\", "/");
                    
                    Debug.WriteLine($"Server dialog returned with name: {serverName}, command: {command}, args: {argsString}");
                    
                    // Parse arguments and ensure --stdio is in the right place
                    var args = argsString.Split(' ');
                    
                    // Check if --stdio is already in the arguments to avoid duplication
                    bool hasStdioFlag = args.Contains("--stdio");
                    List<string> argsList = new List<string>(args);
                    
                    if (!hasStdioFlag)
                    {
                        Debug.WriteLine("--stdio flag not found, adding it...");
                        
                        // Find the right position to insert the --stdio flag (after the package name)
                        int packageIndex = -1;
                        for (int i = 0; i < argsList.Count; i++)
                        {
                            if (argsList[i].Contains("@modelcontextprotocol"))
                            {
                                packageIndex = i;
                                break;
                            }
                        }
                        
                        if (packageIndex >= 0)
                        {
                            // Insert after the package name
                            argsList.Insert(packageIndex + 1, "--stdio");
                            Debug.WriteLine($"Added --stdio after package name at index {packageIndex + 1}");
                        }
                        else
                        {
                            // If package name not found, insert after -y if it exists
                            int yIndex = argsList.IndexOf("-y");
                            if (yIndex >= 0)
                            {
                                argsList.Insert(yIndex + 1, "--stdio");
                                Debug.WriteLine($"Added --stdio after -y at index {yIndex + 1}");
                            }
                            else
                            {
                                // Last resort: insert at the beginning
                                argsList.Insert(0, "--stdio");
                                Debug.WriteLine("Added --stdio at the beginning");
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("--stdio flag already present");
                    }
                    
                    Debug.WriteLine($"Final arguments: {string.Join(" ", argsList)}");
                    
                    // Create a new server client with the properly formatted arguments
                    var serverClient = new StdioMCPServerClient(command, argsList.ToArray(), Path.GetTempPath(), ServiceProvider.GetService<IDebugLogger>());
                    
                    // Register with the client factory
                    _mcpClientFactory.RegisterMCPServer(serverName, serverClient);
                    
                    // Update or add to the UI
                    var existingServer = Servers.FirstOrDefault(s => s.Name == serverName);
                    if (existingServer != null)
                    {
                        existingServer.ServerClient = serverClient;
                        existingServer.Command = command;
                        existingServer.Args = argsList.ToArray();
                    }
                    else
                    {
                        var serverViewModel = new MCPServerViewModel
                        {
                            Name = serverName,
                            Command = command,
                            Args = argsList.ToArray(),
                            ServerClient = serverClient,
                            IsRunning = false
                        };
                        
                        Servers.Add(serverViewModel);
                    }
                    
                    // Save the MCP server configuration
                    Debug.WriteLine($"Updated server: {serverName}");
                    
                    // Save servers to configuration
                    SaveServersToConfiguration();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error editing server: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Error editing server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        // Fixed SaveServersToConfiguration method
        private void SaveServersToConfiguration()
        {
            try
            {
                Debug.WriteLine("Starting to save server configuration");
                
                // Create a MCPServersConfig object
                var config = new Services.MCP.MCPServerRegistration.MCPServersConfig
                {
                    McpServers = new Dictionary<string, Services.MCP.MCPServerRegistration.MCPServerConfig>()
                };
                
                // Add each server to the configuration
                foreach (var server in Servers)
                {
                    Debug.WriteLine($"Saving server: {server.Name}");
                    
                    // Process the arguments to ensure --stdio is included properly without duplication
                    List<string> args = new List<string>();
                    
                    if (server.Args != null && server.Args.Length > 0)
                    {
                        // Create a clean copy of args without duplicating --stdio
                        args = server.Args.Where(a => a != "--stdio").ToList();
                        Debug.WriteLine($"Original args (without --stdio): {string.Join(" ", args)}");
                    }
                    else
                    {
                        // Default args
                        args.Add("-y");
                        args.Add("@modelcontextprotocol/server-filesystem");
                        args.Add("C:/");
                        Debug.WriteLine("Using default args");
                    }
                    
                    // Find where to insert --stdio
                    int insertIndex = -1;
                    for (int i = 0; i < args.Count; i++)
                    {
                        if (args[i].Contains("@modelcontextprotocol"))
                        {
                            insertIndex = i + 1;
                            break;
                        }
                    }
                    
                    if (insertIndex >= 0)
                    {
                        // Insert --stdio after the package name
                        args.Insert(insertIndex, "--stdio");
                        Debug.WriteLine($"Added --stdio after package name at index {insertIndex}");
                    }
                    else 
                    {
                        // If no package name found, add after -y if it exists
                        int yIndex = args.IndexOf("-y");
                        if (yIndex >= 0)
                        {
                            args.Insert(yIndex + 1, "--stdio"); 
                            Debug.WriteLine($"Added --stdio after -y at index {yIndex + 1}");
                        }
                        else
                        {
                            // Add at the beginning as last resort
                            args.Insert(0, "--stdio");
                            Debug.WriteLine("Added --stdio at the beginning");
                        }
                    }
                    
                    // Add to configuration
                    config.McpServers[server.Name] = new Services.MCP.MCPServerRegistration.MCPServerConfig
                    {
                        Command = server.Command ?? "npx",
                        Args = args.ToArray()
                    };
                    
                    Debug.WriteLine($"Saved config for {server.Name}: Command={config.McpServers[server.Name].Command}, Args={string.Join(" ", config.McpServers[server.Name].Args)}");
                }
                
                // Serialize to JSON
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                };
                
                var json = System.Text.Json.JsonSerializer.Serialize(config, options);
                Debug.WriteLine($"Generated server config JSON: {json}");
                
                // Save to settings
                Properties.Settings.Default.MCPServers = json;
                Properties.Settings.Default.Save();
                Debug.WriteLine("Saved to Properties.Settings.Default");
                
                // Also save to .roo folder
                try {
                    var rootDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    var rooPath = System.IO.Path.Combine(rootDir, ".roo");
                    Debug.WriteLine($"Saving to rooPath: {rooPath}");
                    
                    // Create directory if it doesn't exist
                    if (!System.IO.Directory.Exists(rooPath))
                    {
                        try {
                            System.IO.Directory.CreateDirectory(rooPath);
                            Debug.WriteLine($"Created directory: {rooPath}");
                        }
                        catch (Exception ex) {
                            Debug.WriteLine($"Error creating directory: {ex.Message}");
                            // Try an alternative location
                            rooPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                                "AIAgent", ".roo");
                            System.IO.Directory.CreateDirectory(rooPath);
                            Debug.WriteLine($"Using alternative path: {rooPath}");
                        }
                    }
                    
                    // Save to mcp.json file
                    var filePath = System.IO.Path.Combine(rooPath, "mcp.json");
                    Debug.WriteLine($"Saving to file: {filePath}");
                    System.IO.File.WriteAllText(filePath, json);
                    Debug.WriteLine($"Successfully saved to {filePath}");
                }
                catch (Exception ex) {
                    Debug.WriteLine($"Error saving to .roo folder: {ex.Message}");
                }
                
                // Also try to save to the root directory's .roo folder
                try
                {
                    var rootDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", "..", "..");
                    var solutionRooPath = System.IO.Path.Combine(rootDir, ".roo");
                    Debug.WriteLine($"Saving to solution rooPath: {solutionRooPath}");
                    
                    // Create directory if it doesn't exist
                    if (!System.IO.Directory.Exists(solutionRooPath))
                    {
                        System.IO.Directory.CreateDirectory(solutionRooPath);
                        Debug.WriteLine($"Created directory: {solutionRooPath}");
                    }
                    
                    // Save to mcp.json file
                    var solutionFilePath = System.IO.Path.Combine(solutionRooPath, "mcp.json");
                    Debug.WriteLine($"Saving to file: {solutionFilePath}");
                    System.IO.File.WriteAllText(solutionFilePath, json);
                    Debug.WriteLine($"Successfully saved to {solutionFilePath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error saving to solution root .roo folder: {ex.Message}");
                }
                
                Debug.WriteLine("Server configuration saved successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving server configuration: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}