# MCP Server Fix Implementation Guide

This guide details how to apply all the fixes needed to solve both the process execution issue and the server configuration persistence problem in the AIAgent project.

## Problem Summary

1. **Server Execution Issue**: The MCP server processes fail to start because:
   - It's using `C:/` as a working directory, which may lack permissions
   - It's directly calling `npx` without using `cmd.exe /c` wrapper
   - It tries to start servers during registration, which causes failures to cascade

2. **Settings Persistence Issue**: 
   - Modified servers don't appear in the UI after restarting
   - Server settings aren't being properly saved between sessions
   - Duplicate `--stdio` flags appear in configuration

3. **Server UI Population Issue**:
   - Only one server appears in the UI, even when multiple are in the configuration file

## Files to Fix

1. **StdioMCPServerClient.cs** - Fix process execution and working directory issues
2. **MCPServiceRegistration.cs** - Fix server registration and startup deferral
3. **MCPServerRegistration.cs** - Fix server configuration issues and improve error handling
4. **MCPServerManagerViewModel.cs** - Fix settings persistence and UI population

## Step-by-Step Implementation

### 1. Fix StdioMCPServerClient.cs

Open `C:\LLM\Projects\ClaudeTest\AIAgent\AIAgentTest\API_Clients\MCP\StdioMCPServerClient.cs`

Key changes to make:
- Modify the `StartServerAsync()` method to use a reliable system directory instead of `C:/`
- Use "cmd.exe" with "/c" to help find and execute the `npx` command
- Add more extensive error handling and logging

Replace the `StartServerAsync()` method with:

```csharp
public async Task<bool> StartServerAsync()
{
    if (IsConnected)
        return true;

    try
    {
        // Use a reliable system directory instead of a potentially inaccessible one
        string workDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
        
        // Fallbacks if needed
        if (string.IsNullOrEmpty(workDir) || !Directory.Exists(workDir))
        {
            workDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        }
        if (string.IsNullOrEmpty(workDir) || !Directory.Exists(workDir))
        {
            workDir = Path.GetTempPath();
        }
        
        // Use command shell to help find the command
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {_command} {string.Join(" ", _arguments)}",
            WorkingDirectory = workDir,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        
        _logger?.Log($"Starting process with cmd.exe /c {_command} {string.Join(" ", _arguments)}");
        _logger?.Log($"Using working directory: {workDir}");

        _serverProcess = new Process { StartInfo = startInfo };
        _serverProcess.EnableRaisingEvents = true;

        _serverProcess.OutputDataReceived += OnOutputDataReceived;
        _serverProcess.ErrorDataReceived += OnErrorDataReceived;
        _serverProcess.Exited += OnProcessExited;

        if (!_serverProcess.Start())
        {
            _logger?.Log("Failed to start process");
            return false;
        }

        _stdinWriter = new StreamWriter(_serverProcess.StandardInput.BaseStream, Encoding.UTF8)
        {
            AutoFlush = true
        };

        _serverProcess.BeginOutputReadLine();
        _serverProcess.BeginErrorReadLine();

        _logger?.Log("MCP server in stdio mode detected, waiting to ensure it's ready");
        
        // Wait a bit to make sure the process is fully started
        await Task.Delay(3000);
        
        if (_serverProcess.HasExited)
        {
            _logger?.Log("Server process exited prematurely");
            return false;
        }
        
        _isStarted = true;
        _logger?.Log("MCP server started successfully in stdio mode");
        
        // Now immediately try to get tools to verify it's working
        try
        {
            _logger?.Log("Preloading tools to verify server is working");
            var tools = await GetToolsAsync();
            _logger?.Log($"Successfully preloaded {tools.Count} tools from server");
        }
        catch (Exception ex)
        {
            _logger?.Log($"Failed to preload tools: {ex.Message}");
            // Don't fail the server start just because tools failed
        }
        
        return true;
    }
    catch (Exception ex)
    {
        _logger?.Log($"Error starting server: {ex.Message}");
        _logger?.Log($"Stack trace: {ex.StackTrace}");
        await StopServerAsync();
        return false;
    }
}
```

### 2. Fix MCPServiceRegistration.cs

Open `C:\LLM\Projects\ClaudeTest\AIAgent\AIAgentTest\Services\MCP\MCPServiceRegistration.cs`

Key changes to make:
- Modify the `RegisterStdioMCPClient` method to use a reliable directory
- Update the server registration to defer server startup
- Improve error handling to be more resilient

Replace the `RegisterStdioMCPClient` method with:

```csharp
public static IMCPServerClient RegisterStdioMCPClient(string name, string targetDirectory)
{
    var logger = ServiceProvider.GetService<IDebugLogger>();
    var mcpClientFactory = ServiceProvider.GetService<MCPClientFactory>();
    
    if (mcpClientFactory == null)
    {
        logger?.Log("MCP client factory not found, returning null");
        return null;
    }
    
    logger?.Log($"Creating StdioMCPServerClient for '{name}' with directory {targetDirectory}");
    
    // If directory doesn't exist, create it or use a reliable system directory
    if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory))
    {
        logger?.Log($"Target directory does not exist: {targetDirectory}");
        
        // Use a reliable system directory
        targetDirectory = Path.GetTempPath();
        logger?.Log($"Using temp directory instead: {targetDirectory}");
    }

    // Create command and arguments with proper executable path
    string command = "cmd.exe";
    string[] args = new[] { "/c", "npx", "-y", "@modelcontextprotocol/server-filesystem", "--stdio", targetDirectory };

    // Create the client without starting it yet
    var mcpClient = new StdioMCPServerClient(command, args, targetDirectory, logger);
    
    // Register the client without starting it
    mcpClientFactory.RegisterMCPServer(name, mcpClient);
    logger?.Log($"Successfully registered StdioMCPServerClient for '{name}' (deferred startup)");
    
    return mcpClient;
}
```

Also update the RegisterMCPServicesAsync method to use the new pattern:

```csharp
public static async Task RegisterMCPServicesAsync()
{
    // ... existing code ...
    
    // Register MCP servers and properly await
    logger?.Log("Registering MCP servers...");
    try 
    {
        // First use RegisterMCPServers with skipStartup=true to defer startup
        var registeredServers = await MCPServerRegistration.RegisterMCPServersAsync(mcpClientFactory, logger, true);
        logger?.Log("MCP server registration completed.");
        
        // Populate UI with servers regardless of whether they can be started
        var uiViewModel = ServiceProvider.GetService<ViewModels.MCPServerManagerViewModel>();
        if (uiViewModel != null)
        {
            // Force UI refresh
            logger?.Log("Refreshing server UI...");
            uiViewModel.LoadServers(mcpClientFactory);
        }
        
        // Register a default client if none were registered
        var servers = mcpClientFactory.GetRegisteredServerNames();
        if (servers == null || servers.Count == 0)
        {
            logger?.Log("No MCP servers registered. Adding a default MCP server");
            
            // Use temp directory which should always exist with proper permissions
            string targetDir = Path.GetTempPath();
            logger?.Log($"Using target directory: {targetDir}");
            
            // Create and register the stdio client with cmd.exe wrapper
            string command = "cmd.exe";
            string[] args = new[] { "/c", "npx", "-y", "@modelcontextprotocol/server-filesystem", "--stdio", targetDir };
            
            var mcpClient = new StdioMCPServerClient(command, args, targetDir, logger);
            mcpClientFactory.RegisterMCPServer("FileServer", mcpClient);
            logger?.Log("Adding FileServer to UI manually");
            
            // Update UI to show the default server
            if (uiViewModel != null)
            {
                logger?.Log("Refreshing server UI after adding default server...");
                uiViewModel.LoadServers(mcpClientFactory);
            }
        }
        else
        {
            logger?.Log($"Found {servers.Count} registered MCP servers: {string.Join(", ", servers)}");
        }
        
        // Register tools from MCP servers without trying to start them yet
        await MCPServerToolRegistration.RegisterServerToolsAsync(toolRegistry, mcpClientFactory, logger);
    }
    catch (Exception ex)
    {
        logger?.Log($"Error during MCP server registration: {ex.Message}");
        logger?.Log($"Stack trace: {ex.StackTrace}");
    }
}
```

### 3. Fix MCPServerRegistration.cs

Open `C:\LLM\Projects\ClaudeTest\AIAgent\AIAgentTest\Services\MCP\MCPServerRegistration.cs`

Add the new `RegisterMCPServersAsync` method with `skipStartup` parameter and the new `RegisterServerWithFixedParams` method from the `MCPServerRegistration_RegisterMCPServersAsync_Fix.cs` file.

### 4. Fix MCPServerManagerViewModel.cs

Open `C:\LLM\Projects\ClaudeTest\AIAgent\AIAgentTest\ViewModels\MCPServerManagerViewModel.cs`

Add a new `LoadServers` method that takes a factory parameter, and fix the existing methods:

```csharp
/// <summary>
/// Loads the servers from the MCPClientFactory
/// </summary>
public void LoadServers(MCPClientFactory factory = null)
{
    var mcpClientFactory = factory ?? _mcpClientFactory;
    Servers.Clear();
    
    try
    {
        var logger = ServiceProvider.GetService<IDebugLogger>();
        logger?.Log("MCPServerManagerViewModel: LoadServers called");
        
        // Get all registered server names
        var serverNames = mcpClientFactory.GetAllRegisteredServers();
        logger?.Log($"Found {serverNames.Count} registered server names: {string.Join(", ", serverNames)}");
        
        // Create a ViewModel for each server
        foreach (var serverName in serverNames)
        {
            var serverClient = mcpClientFactory.GetMCPServer(serverName);
            if (serverClient != null)
            {
                logger?.Log($"Creating ViewModel for server '{serverName}'");
                
                // Try to get command and args if this is a StdioMCPServerClient
                string command = "npx";
                string[] args = null;
                
                if (serverClient is StdioMCPServerClient stdioClient)
                {
                    // This info is not directly accessible, so defaults are used
                    logger?.Log($"Server '{serverName}' is StdioMCPServerClient");
                }
                
                var serverViewModel = new MCPServerViewModel
                {
                    Name = serverName,
                    Command = command,
                    Args = args,
                    ServerClient = serverClient,
                    IsRunning = false, // Will be updated by RefreshServers
                    IsConnected = false, // Will be updated by RefreshServers
                    LastConnectionAttempt = DateTime.Now
                };
                
                Servers.Add(serverViewModel);
                logger?.Log($"Added server '{serverName}' to UI");
            }
            else
            {
                logger?.Log($"Skipping server '{serverName}' because client is null");
            }
        }
        
        // Refresh the status of all servers
        logger?.Log("Refreshing server statuses");
        RefreshServers();
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Error loading servers: {ex.Message}");
        var logger = ServiceProvider.GetService<IDebugLogger>();
        logger?.Log($"Error loading servers: {ex.Message}");
        logger?.Log($"Stack trace: {ex.StackTrace}");
        MessageBox.Show($"Error loading servers: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
```

Also fix the `SaveServersToConfiguration()` method with the version from `MCPServerManagerViewModel_Fix.cs`.

## Testing the Fixes

After implementing these fixes:

1. Rebuild and start the application
2. Check the MCP Servers tab - you should see all servers from your configuration
3. Try adding a new server - it should now save the configuration properly
4. Restart the application - all servers should still be visible
5. Check if any of the servers display "Connected" status - if NPX is installed, they should work now

## Troubleshooting

If issues persist:

1. Check the logs to see if there are any error messages
2. Verify if NPX is installed and working correctly
3. Try creating a server with an absolute path to NPX (e.g., `C:\Program Files\nodejs\npx.cmd`)
4. Make sure Node.js is properly installed and in your PATH environment variable
