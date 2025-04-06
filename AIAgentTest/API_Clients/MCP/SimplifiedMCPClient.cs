using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AIAgentTest.Services;
using AIAgentTest.Services.Interfaces;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.API_Clients.MCP
{
    /// <summary>
    /// Simplified implementation of IMCPServerClient that communicates with an external MCP server
    /// directly via HTTP, without managing the server process.
    /// </summary>
    public class SimplifiedMCPClient : IMCPServerClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _toolsEndpoint;
        private readonly IDebugLogger _logger;
        private List<ToolDefinition> _cachedTools;
        private bool _isConnected = false;
        private Process _serverProcess;
        
        // Process startup configuration
        private string _command = "npx";
        private string[] _args = new[] { "-y", "@modelcontextprotocol/server-filesystem", "C:\\" };
        
        /// <summary>
        /// Constructor for SimplifiedMCPClient
        /// </summary>
        /// <param name="baseUrl">Base URL of the MCP server (e.g., "http://localhost:3000")</param>
        /// <param name="logger">Optional debug logger</param>
        public SimplifiedMCPClient(string baseUrl, IDebugLogger logger = null)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _logger = logger ?? ServiceProvider.GetService<IDebugLogger>();
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            
            // Define endpoint URLs based on the MCP Inspector patterns
            _toolsEndpoint = $"{_baseUrl}/tools";
        }
        
        /// <summary>
        /// Constructor for SimplifiedMCPClient with server process management
        /// </summary>
        /// <param name="baseUrl">Base URL of the MCP server</param>
        /// <param name="command">Command to start the server (e.g., "npx")</param>
        /// <param name="args">Arguments for the command</param>
        /// <param name="logger">Optional debug logger</param>
        public SimplifiedMCPClient(string baseUrl, string command, string[] args, IDebugLogger logger = null)
        {
            _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _args = args ?? throw new ArgumentNullException(nameof(args));
            _logger = logger ?? ServiceProvider.GetService<IDebugLogger>();
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            
            // Define endpoint URLs based on the MCP Inspector patterns
            _toolsEndpoint = $"{_baseUrl}/tools";
        }

        /// <summary>
        /// Get available tools from the server
        /// </summary>
        public async Task<List<ToolDefinition>> GetToolsAsync()
        {
            // If we already have cached tools, return them
            if (_cachedTools != null && _cachedTools.Count > 0)
            {
                _logger?.Log($"[MCP] Returning {_cachedTools.Count} cached tools");
                return _cachedTools;
            }
            
            // Check server availability
            bool serverAvailable = false;
            try
            {
                serverAvailable = await IsAvailableAsync();
            }
            catch (Exception ex)
            {
                _logger?.Log($"[MCP] Error checking server availability: {ex.Message}");
                serverAvailable = false;
            }
            
            // If server isn't available, go straight to hardcoded tools
            if (!serverAvailable)
            {
                _logger?.Log("[MCP] Server not available, using hardcoded tools list");
                _cachedTools = GetHardcodedTools();
                return _cachedTools;
            }
            
            try
            {
                _logger?.Log($"[MCP] Fetching tools from {_toolsEndpoint}");
                var response = await _httpClient.GetAsync(_toolsEndpoint);
                response.EnsureSuccessStatusCode();
                
                var toolsResponse = await response.Content.ReadFromJsonAsync<ToolsResponse>();
                _cachedTools = toolsResponse.Tools;
                _isConnected = true;
                
                _logger?.Log($"[MCP] Successfully fetched {_cachedTools.Count} tools from server");
                return _cachedTools;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[MCP] Error fetching tools: {ex.Message}");
                // Fall back to hardcoded tools
                _cachedTools = GetHardcodedTools();
                return _cachedTools;
            }
        }

        /// <summary>
        /// Execute a tool on the server
        /// </summary>
        public async Task<object> ExecuteToolAsync(string toolName, object input)
        {
            if (string.IsNullOrEmpty(toolName))
                throw new ArgumentNullException(nameof(toolName));
            
            _logger?.Log($"[MCP] Executing tool: {toolName} with input: {input}");
            
            // Check for server availability first
            bool serverAvailable = false;
            try
            {
                serverAvailable = await IsAvailableAsync();
            }
            catch
            {
                serverAvailable = false;
            }
            
            // If server isn't available, go straight to fallback
            if (!serverAvailable)
            {
                _logger?.Log($"[MCP] Server not available, using fallback implementation");
                var (success, fallbackResult) = await FallbackToolExecutionAsync(toolName, input);
                
                if (success)
                {
                    _logger?.Log($"[MCP] Successfully executed tool {toolName} using fallback implementation");
                    return fallbackResult;
                }
                else
                {
                    _logger?.Log($"[MCP] Failed to execute tool {toolName} using fallback implementation");
                    return new { error = $"Server not available and fallback failed for tool {toolName}" };
                }
            }
            
            // Map tool names to appropriate endpoints
            Dictionary<string, string> endpointMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "read_file", "files" },
                { "write_file", "files" },
                { "edit_file", "files/edit" },
                { "list_directory", "directories" },
                { "directory_tree", "directories/tree" },
                { "create_directory", "directories" },
                { "move_file", "files/move" },
                { "search_files", "search" },
                { "get_file_info", "files/info" },
                { "read_multiple_files", "files/batch" },
                { "list_allowed_directories", "allowed" }
            };
            
            // Find the appropriate endpoint or use the tool name as fallback
            string endpoint = endpointMap.TryGetValue(toolName, out var mappedEndpoint) 
                ? $"{_baseUrl}/{mappedEndpoint}" 
                : $"{_baseUrl}/{toolName.ToLowerInvariant()}";
            
            try
            {
                // Choose the appropriate HTTP method based on the tool
                HttpMethod method = HttpMethod.Post; // Default to POST
                if (toolName.Equals("read_file", StringComparison.OrdinalIgnoreCase) ||
                    toolName.Equals("list_directory", StringComparison.OrdinalIgnoreCase) ||
                    toolName.Equals("get_file_info", StringComparison.OrdinalIgnoreCase) ||
                    toolName.Equals("list_allowed_directories", StringComparison.OrdinalIgnoreCase))
                {
                    method = HttpMethod.Get;
                }
                
                HttpResponseMessage response;
                
                if (method == HttpMethod.Get)
                {
                    // For GET requests, add parameters as query string
                    var queryParams = new StringBuilder();
                    if (input is Dictionary<string, object> dictInput)
                    {
                        bool isFirst = true;
                        foreach (var kvp in dictInput)
                        {
                            queryParams.Append(isFirst ? "?" : "&");
                            queryParams.Append(Uri.EscapeDataString(kvp.Key));
                            queryParams.Append("=");
                            queryParams.Append(Uri.EscapeDataString(kvp.Value?.ToString() ?? ""));
                            isFirst = false;
                        }
                    }
                    
                    string url = endpoint + queryParams.ToString();
                    _logger?.Log($"[MCP] Sending GET request to {url}");
                    response = await _httpClient.GetAsync(url);
                }
                else
                {
                    // For POST requests, send parameters in the body
                    string requestBody = JsonSerializer.Serialize(input);
                    var content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                    
                    _logger?.Log($"[MCP] Sending POST request to {endpoint}");
                    response = await _httpClient.PostAsync(endpoint, content);
                }
                
                response.EnsureSuccessStatusCode();
                
                // Read the response content
                string responseContent = await response.Content.ReadAsStringAsync();
                _logger?.Log($"[MCP] Received response: {responseContent}");
                
                // Parse the JSON response
                var result = JsonSerializer.Deserialize<object>(responseContent);
                _isConnected = true;
                return result;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[MCP] Error executing tool {toolName}: {ex.Message}");
                
                // Try the fallback implementation
                _logger?.Log($"[MCP] Attempting fallback execution for tool: {toolName}");
                var (success, fallbackResult) = await FallbackToolExecutionAsync(toolName, input);
                
                if (success)
                {
                    _logger?.Log($"[MCP] Successfully executed tool {toolName} using fallback implementation");
                    return fallbackResult;
                }
                else
                {
                    _logger?.Log($"[MCP] Failed to execute tool {toolName} using fallback implementation");
                    return new { error = $"Error executing tool {toolName}: {ex.Message}" };
                }
            }
        }

        /// <summary>
        /// Check if the server is available
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            if (_isConnected)
                return true;
                
            try
            {
                _logger?.Log($"[MCP] Checking if server is available at {_baseUrl}");
                
                // Set a shorter timeout for availability check
                var timeoutClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                
                // Try multiple endpoints to determine if server is available
                // First try the base URL
                try {
                    var response = await timeoutClient.GetAsync(_baseUrl);
                    // 404 is fine - it means the server is running but that endpoint doesn't exist
                    _isConnected = response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
                } catch {
                    // If that fails, try the tools endpoint
                    try {
                        var response = await timeoutClient.GetAsync(_toolsEndpoint);
                        _isConnected = response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
                    } catch {
                        // Try one more endpoint - the allowed endpoint
                        try {
                            var response = await timeoutClient.GetAsync($"{_baseUrl}/allowed");
                            _isConnected = response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound;
                        } catch {
                            _isConnected = false;
                        }
                    }
                }
                
                if (_isConnected)
                    _logger?.Log($"[MCP] Server is available at {_baseUrl}");
                else
                    _logger?.Log($"[MCP] Server is not available at {_baseUrl}");
                    
                return _isConnected;
            }
            catch (TaskCanceledException)
            {
                _logger?.Log($"[MCP] Server connection timed out at {_baseUrl}");
                return false;
            }
            catch (HttpRequestException ex)
            {
                _logger?.Log($"[MCP] Server is not available: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[MCP] Error checking server availability: {ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Start the server process - for this implementation, starts the MCP server
        /// </summary>
        public async Task<bool> StartServerAsync()
        {
            // If already connected, no need to start
            if (_isConnected)
                return true;
                
            // Check if already started but not connected
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                _logger?.Log("Server process already running, checking if responsive...");
                return await IsAvailableAsync();
            }
            
            _logger?.Log($"Starting MCP server with command: {_command} {string.Join(" ", _args)}");
            
            try
            {
                // Find the actual path to npx if needed
                string commandPath = _command;
                if (_command.Equals("npx", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var checkProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "where", // Windows command to locate executables
                                Arguments = "npx",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                CreateNoWindow = true
                            }
                        };
                        
                        checkProcess.Start();
                        string output = await checkProcess.StandardOutput.ReadToEndAsync();
                        await checkProcess.WaitForExitAsync();
                        
                        if (string.IsNullOrEmpty(output))
                        {
                            _logger?.Log("ERROR: NPX not found in PATH. Please install Node.js and NPM.");
                            return false;
                        }
                        
                        _logger?.Log($"Found NPX at: {output.Trim()}");
                        
                        // Update command to point to the npx.cmd if found
                        var outputLines = output.Trim().Split(Environment.NewLine);
                        string npxCmdPath = outputLines.FirstOrDefault(line => line.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase));
                        
                        if (!string.IsNullOrEmpty(npxCmdPath)) {
                            _logger?.Log($"Using NPX cmd file: {npxCmdPath}");
                            commandPath = npxCmdPath;
                        } else {
                            _logger?.Log($"No .cmd file found, using first path: {outputLines.FirstOrDefault()}");
                            commandPath = outputLines.FirstOrDefault() ?? "npx";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"ERROR checking for NPX: {ex.Message}");
                        // Continue with original command
                    }
                }
                
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                
                // Prepare process info
                _serverProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = commandPath,
                        Arguments = string.Join(" ", _args),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                    }
                };
                
                // Add event handlers for output
                _serverProcess.OutputDataReceived += new DataReceivedEventHandler((sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        _logger?.Log($"MCP Server: {e.Data}");
                        Console.WriteLine($"MCP Server: {e.Data}");
                    }
                });
                
                _serverProcess.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        _logger?.Log($"MCP Server Error: {e.Data}");
                        Console.WriteLine($"MCP Server Error: {e.Data}");
                    }
                });
                
                // Start the process
                _logger?.Log("Starting MCP server process...");
                bool started = _serverProcess.Start();
                
                if (!started)
                {
                    _logger?.Log("Failed to start MCP server process");
                    return false;
                }
                
                // Begin reading output
                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();
                
                // Wait for the server to be available
                _logger?.Log("Waiting for server to become responsive...");
                
                // Give it some time to start up
                await Task.Delay(2000);
                
                // Check if process is still running
                if (_serverProcess.HasExited)
                {
                    _logger?.Log($"Server process exited prematurely with code {_serverProcess.ExitCode}");
                    _logger?.Log($"Error output: {errorBuilder.ToString()}");
                    return false;
                }
                
                // Try to connect
                int retries = 5;
                while (retries > 0)
                {
                    _logger?.Log($"Checking server availability (attempt {6-retries}/5)...");
                    var available = await IsAvailableAsync();
                    
                    if (available)
                    {
                        _logger?.Log("Server is now available!");
                        return true;
                    }
                    
                    await Task.Delay(1000);
                    retries--;
                }
                
                // If we get here, the server didn't respond in time
                _logger?.Log("Server did not become responsive in the allotted time");
                _logger?.Log($"Server output: {outputBuilder.ToString()}");
                _logger?.Log($"Error output: {errorBuilder.ToString()}");
                
                // Check if the output indicates stdio mode
                if (errorBuilder.ToString().Contains("running on stdio") || outputBuilder.ToString().Contains("running on stdio"))
                {
                    _logger?.Log("The server is running in stdio mode instead of HTTP mode. Try adding --http to the command arguments.");
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger?.Log($"Error starting server: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stop the server process if it's running
        /// </summary>
        public void StopServer()
        {
            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                try
                {
                    _logger?.Log("Stopping MCP server process...");
                    _serverProcess.Kill(entireProcessTree: true);
                    _serverProcess.WaitForExit(5000); // Wait up to 5 seconds
                    
                    if (!_serverProcess.HasExited)
                    {
                        _logger?.Log("MCP server process did not exit gracefully after Kill");
                    }
                    else
                    {
                        _logger?.Log("MCP server process stopped successfully");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Log($"Error stopping MCP server: {ex.Message}");
                }
                finally
                {
                    _serverProcess?.Dispose();
                    _serverProcess = null;
                }
            }
            
            _isConnected = false;
        }

        /// <summary>
        /// Clean up resources when disposed
        /// </summary>
        public void Dispose()
        {
            StopServer();
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }

        #region Helper Methods and Types

        /// <summary>
        /// Response format for tools list from server
        /// </summary>
        private class ToolsResponse
        {
            [JsonPropertyName("tools")]
            public List<ToolDefinition> Tools { get; set; }
        }

        /// <summary>
        /// Response format for file content
        /// </summary>
        private class FileContentResponse
        {
            [JsonPropertyName("content")]
            public string Content { get; set; }
        }

        /// <summary>
        /// Response format for directory listing
        /// </summary>
        private class DirectoryListingResponse
        {
            [JsonPropertyName("entries")]
            public List<string> Entries { get; set; }
        }

        /// <summary>
        /// Fallback tool execution for when server communication fails
        /// </summary>
        private async Task<(bool success, object result)> FallbackToolExecutionAsync(string toolName, object input)
        {
            try
            {
                // Parse input to Dictionary
                Dictionary<string, object> inputDict;
                if (input is JsonElement jsonElement)
                {
                    inputDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                }
                else if (input is Dictionary<string, object> dictObj)
                {
                    inputDict = dictObj;
                }
                else
                {
                    string jsonString = JsonSerializer.Serialize(input);
                    inputDict = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
                }
                
                // Execute basic functionality for common tools
                switch (toolName)
                {
                    case "read_file":
                        {
                            string filePath = inputDict["path"].ToString();
                            if (System.IO.File.Exists(filePath))
                            {
                                var content = await System.IO.File.ReadAllTextAsync(filePath);
                                return (true, new { content });
                            }
                            return (true, new { error = $"File not found: {filePath}" });
                        }
                    
                    case "list_directory":
                        {
                            string dirPath = inputDict["path"].ToString();
                            if (System.IO.Directory.Exists(dirPath))
                            {
                                var entries = new List<string>();
                                
                                foreach (var file in System.IO.Directory.GetFiles(dirPath))
                                    entries.Add($"[FILE] {System.IO.Path.GetFileName(file)}");
                                
                                foreach (var dir in System.IO.Directory.GetDirectories(dirPath))
                                    entries.Add($"[DIR] {System.IO.Path.GetFileName(dir)}");
                                
                                return (true, new { entries });
                            }
                            return (true, new { error = $"Directory not found: {dirPath}" });
                        }
                    
                    case "write_file":
                        {
                            string writePath = inputDict["path"].ToString();
                            string fileContent = inputDict["content"].ToString();
                            
                            string directory = System.IO.Path.GetDirectoryName(writePath);
                            if (!string.IsNullOrEmpty(directory))
                                System.IO.Directory.CreateDirectory(directory);
                            
                            await System.IO.File.WriteAllTextAsync(writePath, fileContent);
                            return (true, new { success = true, message = $"File written to {writePath}" });
                        }
                        
                    case "list_allowed_directories":
                        {
                            // Just return a list with the current directory as a default
                            var allowedDirs = new List<string> { System.IO.Directory.GetCurrentDirectory() };
                            return (true, new { directories = allowedDirs });
                        }
                    
                    default:
                        return (false, new { error = $"Tool '{toolName}' not implemented in fallback mode" });
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[MCP] Error in fallback execution: {ex.Message}");
                return (false, new { error = $"Fallback execution error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Provides hardcoded tool definitions as a fallback
        /// </summary>
        private List<ToolDefinition> GetHardcodedTools()
        {
            _logger?.Log("[MCP] Using hardcoded tool definitions as fallback");
            return new List<ToolDefinition>
            {
                // Tool 1: read_file
                new ToolDefinition
                {
                    Name = "read_file",
                    Description = "Read the complete contents of a file from the file system. Handles various text encodings and provides detailed error messages if the file cannot be read. Use this tool when you need to examine the contents of a single file. Only works within allowed directories.",
                    Input = new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>
                            {
                                { "path", new Dictionary<string, string>
                                    {
                                        { "type", "string" },
                                        { "description", "Path to the file" }
                                    }
                                }
                            }
                        },
                        { "required", new[] { "path" } }
                    },
                    Tags = new[] { "Filesystem", "MCP" },
                    Metadata = new Dictionary<string, object>
                    {
                        { "server_name", "FileServer" },
                        { "server_type", "filesystem" }
                    }
                },
                
                // Tool 2: read_multiple_files
                new ToolDefinition
                {
                    Name = "read_multiple_files",
                    Description = "Read the contents of multiple files simultaneously. This is more efficient than reading files one by one when you need to analyze or compare multiple files. Each file's content is returned with its path as a reference. Failed reads for individual files won't stop the entire operation. Only works within allowed directories.",
                    Input = new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>
                            {
                                { "paths", new Dictionary<string, object>
                                    {
                                        { "type", "array" },
                                        { "items", new Dictionary<string, string>
                                            {
                                                { "type", "string" }
                                            }
                                        },
                                        { "description", "Array of file paths to read" }
                                    }
                                }
                            }
                        },
                        { "required", new[] { "paths" } }
                    },
                    Tags = new[] { "Filesystem", "MCP" },
                    Metadata = new Dictionary<string, object>
                    {
                        { "server_name", "FileServer" },
                        { "server_type", "filesystem" }
                    }
                },
                
                // Tool 3: write_file
                new ToolDefinition
                {
                    Name = "write_file",
                    Description = "Create a new file or completely overwrite an existing file with new content. Use with caution as it will overwrite existing files without warning. Handles text content with proper encoding. Only works within allowed directories.",
                    Input = new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>
                            {
                                { "path", new Dictionary<string, string>
                                    {
                                        { "type", "string" },
                                        { "description", "Path to the file" }
                                    }
                                },
                                { "content", new Dictionary<string, string>
                                    {
                                        { "type", "string" },
                                        { "description", "Content to write" }
                                    }
                                }
                            }
                        },
                        { "required", new[] { "path", "content" } }
                    },
                    Tags = new[] { "Filesystem", "MCP" },
                    Metadata = new Dictionary<string, object>
                    {
                        { "server_name", "FileServer" },
                        { "server_type", "filesystem" }
                    }
                },
                
                // Tool 4: list_directory
                new ToolDefinition
                {
                    Name = "list_directory",
                    Description = "Get a detailed listing of all files and directories in a specified path. Results clearly distinguish between files and directories with [FILE] and [DIR] prefixes. This tool is essential for understanding directory structure and finding specific files within a directory. Only works within allowed directories.",
                    Input = new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>
                            {
                                { "path", new Dictionary<string, string>
                                    {
                                        { "type", "string" },
                                        { "description", "Path to the directory" }
                                    }
                                }
                            }
                        },
                        { "required", new[] { "path" } }
                    },
                    Tags = new[] { "Filesystem", "MCP" },
                    Metadata = new Dictionary<string, object>
                    {
                        { "server_name", "FileServer" },
                        { "server_type", "filesystem" }
                    }
                },
                
                // Tool 5: list_allowed_directories
                new ToolDefinition
                {
                    Name = "list_allowed_directories",
                    Description = "Returns the list of directories that this server is allowed to access. Use this to understand which directories are available before trying to access files.",
                    Input = new Dictionary<string, object>
                    {
                        { "type", "object" },
                        { "properties", new Dictionary<string, object>() },
                        { "required", new string[] { } }
                    },
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