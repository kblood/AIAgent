using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AIAgentTest.Services;
using AIAgentTest.Services.Interfaces;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.API_Clients.MCP
{
    /// <summary>
    /// Implementation of IMCPServerClient specifically for filesystem operations.
    /// This client starts a Node.js MCP server and communicates with it via HTTP.
    /// </summary>
    public class FileSystemMCPServerClient : IMCPServerClient, IDisposable
    {
        private readonly string _command;
        private readonly string[] _arguments;
        private readonly string _baseUrl;
        private readonly string _toolsEndpoint;
        private readonly string _executeEndpoint;
        private readonly IDebugLogger _logger;
        private readonly HttpClient _httpClient;
        private Process _serverProcess;
        private List<ToolDefinition> _cachedTools;
        private bool _isStarted = false;
        private readonly CancellationTokenSource _processTerminationCts = new CancellationTokenSource();
        
        /// <summary>
        /// Constructor for FileSystemMCPServerClient
        /// </summary>
        /// <param name="command">Command to run the server (e.g., "npx")</param>
        /// <param name="arguments">Arguments for the command</param>
        /// <param name="baseUrl">Base URL for the server (defaults to http://localhost:3000)</param>
        /// <param name="logger">Optional debug logger</param>
        public FileSystemMCPServerClient(
            string command,
            string[] arguments,
            string baseUrl = "http://localhost:3000",
            IDebugLogger logger = null)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _logger = logger ?? ServiceProvider.GetService<IDebugLogger>();
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            
            // Define endpoints
            _toolsEndpoint = $"{_baseUrl}/tools";
            _executeEndpoint = $"{_baseUrl}/execute";
            
            // Start the server in the background
            Task.Run(StartServerAsync);
        }
        
        /// <summary>
        /// Start the Node.js MCP server process
        /// </summary>
        /// <returns>True if the server started successfully</returns>
        public async Task<bool> StartServerAsync()
        {
            if (_isStarted)
            {
                _logger?.Log("[FileSystemMCP] Server already started");
                return true;
            }
            
            _logger?.Log($"[FileSystemMCP] Starting server with command: {_command} {string.Join(" ", _arguments)}");
            
            try
            {
                // Set up process info
                var startInfo = new ProcessStartInfo
                {
                    FileName = _command,
                    Arguments = string.Join(" ", _arguments),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                // Create and start the process
                _serverProcess = new Process { StartInfo = startInfo };
                
                // Set up output handling
                _serverProcess.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger?.Log($"[FileSystemMCP] Server Output: {e.Data}");
                    }
                };
                
                _serverProcess.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        _logger?.Log($"[FileSystemMCP] Server Error: {e.Data}");
                    }
                };
                
                // Start the process
                _serverProcess.Start();
                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();
                
                // Wait for the server to become responsive
                bool serverReady = await WaitForServerReadyAsync(TimeSpan.FromSeconds(30));
                
                if (serverReady)
                {
                    _logger?.Log("[FileSystemMCP] Server started successfully");
                    _isStarted = true;
                    
                    // Setup process termination monitoring
                    MonitorProcessAsync(_processTerminationCts.Token);
                    
                    return true;
                }
                else
                {
                    _logger?.Log("[FileSystemMCP] Server failed to start");
                    // Try to kill the process if it's still running
                    if (!_serverProcess.HasExited)
                    {
                        _serverProcess.Kill();
                    }
                    _serverProcess = null;
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[FileSystemMCP] Error starting server: {ex.Message}");
                
                // Clean up if process was started
                if (_serverProcess != null && !_serverProcess.HasExited)
                {
                    try
                    {
                        _serverProcess.Kill();
                    }
                    catch
                    {
                        // Ignore errors during cleanup
                    }
                    _serverProcess = null;
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// Wait for the server to become responsive
        /// </summary>
        /// <param name="timeout">Timeout period</param>
        /// <returns>True if the server is responsive</returns>
        private async Task<bool> WaitForServerReadyAsync(TimeSpan timeout)
        {
            _logger?.Log($"[FileSystemMCP] Waiting up to {timeout.TotalSeconds} seconds for server to be ready");
            
            var startTime = DateTime.Now;
            var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            
            while (DateTime.Now - startTime < timeout)
            {
                // Check if process has exited
                if (_serverProcess.HasExited)
                {
                    _logger?.Log($"[FileSystemMCP] Server process exited with code {_serverProcess.ExitCode}");
                    return false;
                }
                
                // Try to connect to the server
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, _baseUrl);
                    var response = await httpClient.SendAsync(request);
                    
                    // Either success or 404 means the server is running (404 if endpoint doesn't exist)
                    if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger?.Log("[FileSystemMCP] Server is responding to HTTP requests");
                        return true;
                    }
                }
                catch
                {
                    // Ignore exceptions during connection attempts
                }
                
                // Wait before retrying
                await Task.Delay(500);
            }
            
            _logger?.Log("[FileSystemMCP] Timed out waiting for server to be ready");
            return false;
        }
        
        /// <summary>
        /// Monitor the server process and handle termination
        /// </summary>
        private async Task MonitorProcessAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _serverProcess.WaitForExitAsync(cancellationToken);
                _logger?.Log($"[FileSystemMCP] Server process exited with code {_serverProcess.ExitCode}");
                _isStarted = false;
                _cachedTools = null;
            }
            catch (OperationCanceledException)
            {
                // Operation was canceled, just exit the monitoring
            }
            catch (Exception ex)
            {
                _logger?.Log($"[FileSystemMCP] Error monitoring process: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get available tools from the server
        /// </summary>
        public async Task<List<ToolDefinition>> GetToolsAsync()
        {
            _logger?.Log("[FileSystemMCP] Getting tools from server");
            
            // Return cached tools if available
            if (_cachedTools != null && _cachedTools.Count > 0)
            {
                _logger?.Log($"[FileSystemMCP] Returning {_cachedTools.Count} cached tools");
                return _cachedTools;
            }
            
            // Start server if needed
            if (!_isStarted)
            {
                _logger?.Log("[FileSystemMCP] Server not started, attempting to start...");
                bool started = await StartServerAsync();
                if (!started)
                {
                    _logger?.Log("[FileSystemMCP] Failed to start server, falling back to hardcoded tools");
                    _cachedTools = GetHardcodedTools();
                    return _cachedTools;
                }
            }
            
            try
            {
                // Query the server's tool endpoint
                var response = await _httpClient.GetAsync(_toolsEndpoint);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                _logger?.Log($"[FileSystemMCP] Received tools list: {content}");
                
                // Parse the tools list
                try
                {
                    var toolsResponse = JsonSerializer.Deserialize<ServerToolsResponse>(content);
                    if (toolsResponse?.Tools != null && toolsResponse.Tools.Count > 0)
                    {
                        _logger?.Log($"[FileSystemMCP] Found {toolsResponse.Tools.Count} tools");
                        _cachedTools = toolsResponse.Tools;
                        
                        // Post-process tools to ensure they have proper metadata
                        foreach (var tool in _cachedTools)
                        {
                            if (tool.Metadata == null)
                            {
                                tool.Metadata = new Dictionary<string, object>();
                            }
                            
                            if (!tool.Metadata.ContainsKey("server_name"))
                            {
                                tool.Metadata["server_name"] = "FileServer";
                            }
                            
                            if (!tool.Metadata.ContainsKey("server_type"))
                            {
                                tool.Metadata["server_type"] = "filesystem";
                            }
                            
                            // Add a standard tag if missing
                            if (tool.Tags == null || tool.Tags.Length == 0)
                            {
                                tool.Tags = new[] { "Filesystem", "MCP" };
                            }
                        }
                        
                        return _cachedTools;
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger?.Log($"[FileSystemMCP] Error parsing tools response: {jsonEx.Message}");
                }
                
                // Fall back to hardcoded tools if parsing fails
                _logger?.Log("[FileSystemMCP] Failed to parse tools from server, falling back to hardcoded tools");
                _cachedTools = GetHardcodedTools();
                return _cachedTools;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[FileSystemMCP] Error getting tools from server: {ex.Message}");
                _cachedTools = GetHardcodedTools();
                return _cachedTools;
            }
        }
        
        /// <summary>
        /// Execute a tool on the server
        /// </summary>
        public async Task<object> ExecuteToolAsync(string toolName, object input)
        {
            _logger?.Log($"[FileSystemMCP] Executing tool: {toolName}");
            
            // Start server if needed
            if (!_isStarted)
            {
                _logger?.Log("[FileSystemMCP] Server not started, attempting to start...");
                bool started = await StartServerAsync();
                if (!started)
                {
                    return new { error = "Failed to start server" };
                }
            }
            
            try
            {
                // Prepare request body
                var requestBody = new ToolExecutionRequest
                {
                    Tool = toolName,
                    Input = input
                };
                
                // Send request to the server
                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                _logger?.Log($"[FileSystemMCP] Sending request to execute tool {toolName}: {jsonContent}");
                
                var response = await _httpClient.PostAsync(_executeEndpoint, content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger?.Log($"[FileSystemMCP] Received response: {responseContent}");
                
                // Parse the response
                try
                {
                    var result = JsonSerializer.Deserialize<object>(responseContent);
                    return result;
                }
                catch (JsonException jsonEx)
                {
                    _logger?.Log($"[FileSystemMCP] Error parsing response: {jsonEx.Message}");
                    return new { error = $"Error parsing response: {jsonEx.Message}" };
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[FileSystemMCP] Error executing tool: {ex.Message}");
                
                // Attempt fallback execution
                var (success, fallbackResult) = await FallbackToolExecutionAsync(toolName, input);
                if (success)
                {
                    _logger?.Log("[FileSystemMCP] Fallback execution succeeded");
                    return fallbackResult;
                }
                
                return new { error = $"Error executing tool: {ex.Message}" };
            }
        }
        
        /// <summary>
        /// Check if the server is available
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            if (_isStarted)
            {
                return true;
            }
            
            return await StartServerAsync();
        }
        
        /// <summary>
        /// Stop the server
        /// </summary>
        public void StopServer()
        {
            _logger?.Log("[FileSystemMCP] Stopping server");
            
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                try
                {
                    // Cancel process monitoring
                    _processTerminationCts.Cancel();
                    
                    // Don't wait for exit - this can cause the app to hang
                    try
                    {
                        // Kill the process without waiting
                        _serverProcess.Kill();
                        
                        // Start a background task to monitor the process exit
                        Task.Run(() => {
                            try 
                            {
                                // Check if the process exits within 2 seconds
                                if (!_serverProcess.WaitForExit(2000))
                                {
                                    _logger?.Log("[FileSystemMCP] Server did not exit after 2 seconds");
                                    try
                                    {
                                        // Try to kill it more forcefully
                                        System.Diagnostics.Process.Start("taskkill", $"/F /PID {_serverProcess.Id}");
                                    }
                                    catch (Exception ex2)
                                    {
                                        _logger?.Log($"[FileSystemMCP] Error force killing process: {ex2.Message}");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger?.Log($"[FileSystemMCP] Error in background process monitoring: {ex.Message}");
                            }
                        });
                    }
                    catch (Exception killEx)
                    {
                        _logger?.Log($"[FileSystemMCP] Error killing process: {killEx.Message}");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[FileSystemMCP] Error stopping server: {ex.Message}");
                }
                finally
                {
                    // Set these immediately rather than waiting for exit
                    _serverProcess = null;
                    _isStarted = false;
                    _logger?.Log("[FileSystemMCP] Server marked as stopped");
                }
            }
            else
            {
                _logger?.Log("[FileSystemMCP] Server process already exited or null");
                _serverProcess = null;
                _isStarted = false;
            }
        }
        
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            StopServer();
            _httpClient.Dispose();
            _processTerminationCts.Dispose();
            GC.SuppressFinalize(this);
        }
        
        #region Helper Types and Methods
        
        private class ServerToolsResponse
        {
            [JsonPropertyName("tools")]
            public List<ToolDefinition> Tools { get; set; }
        }
        
        private class ToolExecutionRequest
        {
            [JsonPropertyName("tool")]
            public string Tool { get; set; }
            
            [JsonPropertyName("input")]
            public object Input { get; set; }
        }
        
        /// <summary>
        /// Fallback tool execution for when server communication fails
        /// </summary>
        private async Task<(bool success, object result)> FallbackToolExecutionAsync(string toolName, object input)
        {
            _logger?.Log($"[FileSystemMCP] Attempting fallback execution for tool: {toolName}");
            
            try
            {
                // Convert input to dictionary
                Dictionary<string, object> inputDict;
                if (input is JsonElement jsonElement)
                {
                    inputDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                }
                else if (input is Dictionary<string, object> dict)
                {
                    inputDict = dict;
                }
                else
                {
                    string jsonString = JsonSerializer.Serialize(input);
                    inputDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
                }
                
                // Handle basic filesystem operations
                switch (toolName.ToLowerInvariant())
                {
                    case "read_file":
                        {
                            if (!inputDict.TryGetValue("path", out var pathObj) || pathObj == null)
                            {
                                return (true, new { error = "Path parameter is required" });
                            }
                            
                            string path = pathObj.ToString();
                            
                            if (!File.Exists(path))
                            {
                                return (true, new { error = $"File not found: {path}" });
                            }
                            
                            try
                            {
                                var content = await File.ReadAllTextAsync(path);
                                return (true, new { content });
                            }
                            catch (Exception ex)
                            {
                                return (true, new { error = $"Error reading file: {ex.Message}" });
                            }
                        }
                        
                    case "list_directory":
                        {
                            if (!inputDict.TryGetValue("path", out var pathObj) || pathObj == null)
                            {
                                return (true, new { error = "Path parameter is required" });
                            }
                            
                            string path = pathObj.ToString();
                            
                            if (!Directory.Exists(path))
                            {
                                return (true, new { error = $"Directory not found: {path}" });
                            }
                            
                            try
                            {
                                var entries = new List<object>();
                                
                                foreach (var dir in Directory.GetDirectories(path))
                                {
                                    entries.Add(new 
                                    { 
                                        name = Path.GetFileName(dir), 
                                        type = "directory", 
                                        path = dir,
                                        displayName = $"[DIR] {Path.GetFileName(dir)}"
                                    });
                                }
                                
                                foreach (var file in Directory.GetFiles(path))
                                {
                                    var fileInfo = new FileInfo(file);
                                    entries.Add(new 
                                    { 
                                        name = fileInfo.Name, 
                                        type = "file", 
                                        path = file,
                                        size = fileInfo.Length,
                                        displayName = $"[FILE] {fileInfo.Name} ({FormatFileSize(fileInfo.Length)})"
                                    });
                                }
                                
                                return (true, new { entries });
                            }
                            catch (Exception ex)
                            {
                                return (true, new { error = $"Error listing directory: {ex.Message}" });
                            }
                        }
                        
                    case "list_allowed_directories":
                        {
                            var allowedDirs = new List<object>();
                            
                            // Add some default allowed directories
                            allowedDirs.Add(new { path = AppDomain.CurrentDomain.BaseDirectory, exists = true });
                            allowedDirs.Add(new { path = Path.GetTempPath(), exists = true });
                            allowedDirs.Add(new { path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), exists = true });
                            
                            return (true, new { directories = allowedDirs });
                        }
                        
                    default:
                        return (false, new { error = $"Tool {toolName} not supported in fallback mode" });
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[FileSystemMCP] Error in fallback execution: {ex.Message}");
                return (false, new { error = $"Error in fallback execution: {ex.Message}" });
            }
        }
        
        /// <summary>
        /// Format a file size in bytes to a human-readable string
        /// </summary>
        private string FormatFileSize(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (byteCount == 0)
                return "0" + suf[0];
                
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            
            return $"{(Math.Sign(byteCount) * num).ToString("0.##")} {suf[place]}";
        }
        
        /// <summary>
        /// Get hardcoded tool definitions as a fallback
        /// </summary>
        private List<ToolDefinition> GetHardcodedTools()
        {
            _logger?.Log("[FileSystemMCP] Using hardcoded tool definitions");
            
            return new List<ToolDefinition>
            {
                // Tool 1: read_file
                new ToolDefinition
                {
                    Name = "read_file",
                    Description = "Read the complete contents of a file from the file system. Handles various text encodings and provides detailed error messages if the file cannot be read. Use this tool when you need to examine the contents of a single file. Only works within allowed directories.",
                    Schema = JsonSerializer.Serialize(new
                    {
                        type = "object",
                        properties = new
                        {
                            path = new
                            {
                                type = "string"
                            }
                        },
                        required = new[] { "path" },
                        additionalProperties = false
                    }),
                    Tags = new[] { "Filesystem", "MCP" },
                    Metadata = new Dictionary<string, object>
                    {
                        { "server_name", "FileServer" },
                        { "server_type", "filesystem" }
                    }
                },
                
                // Tool 2: list_directory
                new ToolDefinition
                {
                    Name = "list_directory",
                    Description = "Get a detailed listing of all files and directories in a specified path. Results clearly distinguish between files and directories with [FILE] and [DIR] prefixes. This tool is essential for understanding directory structure and finding specific files within a directory. Only works within allowed directories.",
                    Schema = JsonSerializer.Serialize(new
                    {
                        type = "object",
                        properties = new
                        {
                            path = new
                            {
                                type = "string"
                            }
                        },
                        required = new[] { "path" },
                        additionalProperties = false
                    }),
                    Tags = new[] { "Filesystem", "MCP" },
                    Metadata = new Dictionary<string, object>
                    {
                        { "server_name", "FileServer" },
                        { "server_type", "filesystem" }
                    }
                },
                
                // Tool 3: list_allowed_directories
                new ToolDefinition
                {
                    Name = "list_allowed_directories",
                    Description = "Returns the list of directories that this server is allowed to access. Use this to understand which directories are available before trying to access files.",
                    Schema = JsonSerializer.Serialize(new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[] { },
                        additionalProperties = false
                    }),
                    Tags = new[] { "Filesystem", "MCP" },
                    Metadata = new Dictionary<string, object>
                    {
                        { "server_name", "FileServer" },
                        { "server_type", "filesystem" }
                    }
                }
            };
        }
        
        #endregion
    }
}
