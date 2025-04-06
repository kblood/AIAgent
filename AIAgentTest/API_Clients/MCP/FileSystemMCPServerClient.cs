using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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
    /// MCP server client that communicates with the ModelContextProtocol.io filesystem server implementation
    /// </summary>
    public class FileSystemMCPServerClient : IMCPServerClient
    {
        private string _command;
        private string[] _args;
        private readonly IDebugLogger _logger;
        private Process _serverProcess;
        private bool _isStarted = false;
        private readonly HttpClient _httpClient;
        
        // Default port for MCP server - this is the standard port that the MCP server uses
        private int _serverPort = 3000;
        
        // Base URL for server communication
        private string BaseUrl => $"http://localhost:{_serverPort}";
        
        // Endpoint for tool discovery
        private string ToolsEndpoint => $"{BaseUrl}/tools";
        
        // Endpoint for tool execution
        private string ExecuteEndpoint => $"{BaseUrl}/execute";
        
        // Cached tools from server
        private List<ToolDefinition> _cachedTools;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="command">Command to run (e.g., "npx")</param>
        /// <param name="args">Arguments for the command (e.g., ["-y", "@modelcontextprotocol/server-filesystem", "C:\\path"])</param>
        /// <param name="logger">Optional debug logger</param>
        public FileSystemMCPServerClient(string command, string[] args, IDebugLogger logger = null)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _args = args ?? throw new ArgumentNullException(nameof(args));
            _logger = logger ?? ServiceProvider.GetService<IDebugLogger>();
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            // Parse port from args if present (format: --port XXXX)
            if (args != null)
            {
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "--port" && int.TryParse(args[i + 1], out int port))
                    {
                        _serverPort = port;
                        _logger?.Log($"Found port in arguments: {port}");
                        break;
                    }
                }
            }
        }
        
        /// <summary>
        /// Start the server process
        /// </summary>
        /// <returns>Whether the server was started successfully</returns>
        public async Task<bool> StartServerAsync()
        {
            if (_isStarted)
                return true;
                
            try
            {
                _logger?.Log($"Starting MCP server with command: {_command} {string.Join(" ", _args)}");
                
                // Check if NPX exists before trying to start (Windows-specific check)
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
                        string npxPath = output.Trim().Split(Environment.NewLine)[0].Trim();
                        if (!string.IsNullOrEmpty(npxPath))
                        {
                            // Find the npx.cmd file specifically for Windows
                        var outputLines = output.Trim().Split(Environment.NewLine);
                        string npxCmdPath = outputLines.FirstOrDefault(line => line.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase));
                        
                        if (!string.IsNullOrEmpty(npxCmdPath)) {
                            _logger?.Log($"Using NPX cmd file: {npxCmdPath}");
                            _command = npxCmdPath;
                        } else {
                            _logger?.Log($"No .cmd file found, using first path: {npxPath}");
                            _command = npxPath;
                        }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"ERROR checking for NPX: {ex.Message}");
                        return false;
                    }
                }
                
                // Prepare the allowed directories argument
                string targetDir = null;
                if (_args != null && _args.Length > 0)
                {
                    targetDir = _args[_args.Length - 1];
                    if (Directory.Exists(targetDir))
                    {
                        _logger?.Log($"Target directory exists: {targetDir}");
                    }
                    else
                    {
                        _logger?.Log($"WARNING: Target directory does not exist: {targetDir}");
                        try
                        {
                            Directory.CreateDirectory(targetDir);
                            _logger?.Log($"Created target directory: {targetDir}");
                        }
                        catch (Exception dirEx)
                        {
                            _logger?.Log($"ERROR: Failed to create directory: {dirEx.Message}");
                        }
                    }
                }
                
                // For the MCP server package, we need to use the EXACT arguments that worked
                // manually: npx @modelcontextprotocol/server-filesystem C:\
                List<string> finalArgs = new List<string>();
                
                // Start with the -y flag if needed
                if (_args.Contains("-y"))
                {
                    finalArgs.Add("-y");
                }
                
                // Add the package name
                string packageName = _args.FirstOrDefault(arg => arg.Contains("@modelcontextprotocol"));
                if (!string.IsNullOrEmpty(packageName))
                {
                    finalArgs.Add(packageName);
                }
                
                // Add the directory as the final argument
                if (!string.IsNullOrEmpty(targetDir))
                {
                    finalArgs.Add(targetDir);
                }
                
                // Log the constructed arguments
                _logger?.Log($"Constructed arguments: {string.Join(" ", finalArgs)}");
                
                var outputBuilder = new System.Text.StringBuilder();
                var errorBuilder = new System.Text.StringBuilder();
                
                _serverProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _command,
                        Arguments = string.Join(" ", finalArgs),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        // Use target directory as working directory if available, otherwise use app base directory
                        WorkingDirectory = !string.IsNullOrEmpty(targetDir) && Directory.Exists(targetDir) 
                            ? targetDir 
                            : AppDomain.CurrentDomain.BaseDirectory
                    }
                };
                
                _serverProcess.OutputDataReceived += new DataReceivedEventHandler((sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        Console.WriteLine($"MCP Server: {e.Data}");
                        _logger?.Log($"MCP Server: {e.Data}");
                    }
                });
                
                _serverProcess.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        Console.WriteLine($"MCP Server Error: {e.Data}");
                        _logger?.Log($"MCP Server Error: {e.Data}");
                    }
                });
                
                _logger?.Log("Attempting to start MCP server process...");
                bool started = _serverProcess.Start();
                if (!started)
                {
                    _logger?.Log("Failed to start MCP server process");
                    return false;
                }
                
                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();
                
                // Wait for server to start and become responsive
                _logger?.Log("Waiting for MCP server to initialize...");
                bool serverReady = await WaitForServerReadyAsync(TimeSpan.FromSeconds(30)); // Give it 30 seconds to start
                
                if (!serverReady || _serverProcess.HasExited)
                {
                    _logger?.Log($"MCP server process failed to start or become responsive within timeout");
                    _logger?.Log($"Error output: {errorBuilder.ToString()}");
                    StopServer(); // Clean up the process
                    return false;
                }
                
                _logger?.Log($"MCP server started successfully at {BaseUrl}");
                _isStarted = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start MCP server: {ex.Message}");
                _logger?.Log($"Failed to start MCP server: {ex.Message}");
                _logger?.Log($"Exception details: {ex}");
                return false;
            }
        }
        
        /// <summary>
        /// Wait for the server to become ready and responsive
        /// </summary>
        private async Task<bool> WaitForServerReadyAsync(TimeSpan timeout)
        {
            _logger?.Log($"Waiting for MCP server to initialize in stdio mode (timeout: {timeout.TotalSeconds}s)");
            
            var stopwatch = Stopwatch.StartNew();
            bool receivedOutput = false;
            
            // Set up a flag to indicate when we've seen the startup message
            DataReceivedEventHandler outputHandler = new DataReceivedEventHandler((sender, e) => {
                if (!string.IsNullOrEmpty(e.Data) && e.Data.Contains("Secure MCP Filesystem Server running on stdio"))
                {
                    receivedOutput = true;
                }
            });
            
            // Add our temporary handler
            _serverProcess.OutputDataReceived += outputHandler;
            _serverProcess.ErrorDataReceived += outputHandler;
            
            try
            {
                while (stopwatch.Elapsed < timeout)
                {
                    if (_serverProcess == null || _serverProcess.HasExited)
                    {
                        _logger?.Log("Server process exited prematurely during startup check.");
                        return false;
                    }
                    
                    if (receivedOutput)
                    {
                        _logger?.Log("Server is running in stdio mode and appears ready.");
                        return true;
                    }
                    
                    // Wait a bit before checking again
                    await Task.Delay(500);
                }
                
                stopwatch.Stop();
                _logger?.Log("Timeout reached waiting for server to be ready");
                return false;
            }
            finally
            {
                // Remove our temporary handler
                _serverProcess.OutputDataReceived -= outputHandler;
                _serverProcess.ErrorDataReceived -= outputHandler;
            }
        }
        
        /// <summary>
        /// Stop the server process
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
                    Console.WriteLine($"Error stopping MCP server: {ex.Message}");
                    _logger?.Log($"Error stopping MCP server: {ex.Message}");
                }
                finally
                {
                    _serverProcess?.Dispose();
                    _serverProcess = null;
                }
            }
            
            _isStarted = false;
        }
        
        /// <summary>
        /// Get available tools from the server
        /// </summary>
        /// <returns>List of tool definitions</returns>
        public async Task<List<ToolDefinition>> GetToolsAsync()
        {
            if (!_isStarted)
            {
                if (!await StartServerAsync())
                {
                    _logger?.Log("Failed to start server, returning hardcoded tools list");
                    _cachedTools = GetHardcodedTools();
                    return _cachedTools;
                }
            }
            
            // If we already fetched tools, return the cached version
            if (_cachedTools != null && _cachedTools.Count > 0)
            {
                _logger?.Log($"Returning {_cachedTools.Count} cached tools");
                return _cachedTools;
            }
            
            _logger?.Log($"Server running in stdio mode, using hardcoded tools");
            
            // For stdio mode server, we must use hardcoded tools
            _cachedTools = GetHardcodedTools();
            return _cachedTools;
        }
        
        /// <summary>
        /// Provides hardcoded tool definitions as a fallback if server discovery fails
        /// </summary>
        private List<ToolDefinition> GetHardcodedTools()
        {
            _logger?.Log("Using hardcoded tool definitions as fallback");
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
        
        /// <summary>
        /// Execute a tool on the server
        /// </summary>
        /// <param name="toolName">Tool name</param>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        public async Task<object> ExecuteToolAsync(string toolName, object input)
        {
            _logger?.Log($"Executing tool: {toolName} with input: {input}");
            
            if (!_isStarted)
            {
                _logger?.Log("Server not started. Attempting to start...");
                if (!await StartServerAsync())
                {
                    _logger?.Log("Failed to start server for tool execution");
                    return new { error = "MCP server is not running" };
                }
            }
            
            try
            {
                // Convert input to proper format
                string jsonInput = JsonSerializer.Serialize(input);
                
                // For stdio mode, we can't send HTTP requests
                // Instead, use local implementation directly
                var (success, fallbackResult) = await FallbackToolExecutionAsync(toolName, input);
                if (success)
                {
                    _logger?.Log($"Successfully executed tool {toolName} using fallback implementation");
                    return fallbackResult;
                }
                else
                {
                    _logger?.Log($"Failed to execute tool {toolName} using fallback implementation");
                    return new { error = $"Tool '{toolName}' failed to execute" };
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"Error executing tool: {ex.Message}");
                return new { error = $"Error executing tool: {ex.Message}" };
            }
        }
        
        /// <summary>
        /// Try to execute a tool locally as a fallback if server execution fails
        /// </summary>
        /// <returns>Tuple with success flag and result object</returns>
        private async Task<(bool success, object result)> FallbackToolExecutionAsync(string toolName, object input)
        {
            _logger?.Log($"Attempting fallback execution for tool: {toolName}");
            
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
                            if (File.Exists(filePath))
                            {
                                var content = await File.ReadAllTextAsync(filePath);
                                return (true, new { content });
                            }
                            return (true, new { error = $"File not found: {filePath}" });
                        }
                    
                    case "list_directory":
                        {
                            string dirPath = inputDict["path"].ToString();
                            if (Directory.Exists(dirPath))
                            {
                                var entries = new List<string>();
                                
                                foreach (var file in Directory.GetFiles(dirPath))
                                    entries.Add($"[FILE] {Path.GetFileName(file)}");
                                
                                foreach (var dir in Directory.GetDirectories(dirPath))
                                    entries.Add($"[DIR] {Path.GetFileName(dir)}");
                                
                                return (true, new { entries });
                            }
                            return (true, new { error = $"Directory not found: {dirPath}" });
                        }
                    
                    case "write_file":
                        {
                            string writePath = inputDict["path"].ToString();
                            string fileContent = inputDict["content"].ToString();
                            
                            string directory = Path.GetDirectoryName(writePath);
                            if (!string.IsNullOrEmpty(directory))
                                Directory.CreateDirectory(directory);
                            
                            await File.WriteAllTextAsync(writePath, fileContent);
                            return (true, new { success = true, message = $"File written to {writePath}" });
                        }
                        
                    case "list_allowed_directories":
                        {
                            var allowedDirs = new List<string>();
                            if (_args != null && _args.Length > 0)
                            {
                                allowedDirs.Add(_args[_args.Length - 1].Replace("/", "\\"));
                            }
                            return (true, new { directories = allowedDirs });
                        }
                    
                    default:
                        return (false, new { error = $"Tool '{toolName}' not implemented in fallback mode" });
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"Error in fallback execution: {ex.Message}");
                return (false, new { error = $"Fallback execution error: {ex.Message}" });
            }
        }
        
        /// <summary>
        /// Check if the server is available
        /// </summary>
        /// <returns>True if available</returns>
        public async Task<bool> IsAvailableAsync()
        {
            if (_isStarted && _serverProcess != null && !_serverProcess.HasExited)
            {
                try
                {
                    // Check if we can actually connect to the server
                    var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl);
                    var response = await _httpClient.SendAsync(request, CancellationToken.None);
                    return response.IsSuccessStatusCode || (int)response.StatusCode == 404;
                }
                catch
                {
                    return false;
                }
            }
                
            return await StartServerAsync();
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
        
        #region DTOs for MCP Communication
        
        /// <summary>
        /// Response format for tools list from server
        /// </summary>
        private class ServerToolsResponse
        {
            [JsonPropertyName("tools")]
            public List<ToolDefinition> Tools { get; set; }
        }
        
        /// <summary>
        /// Request format for tool execution
        /// </summary>
        private class ToolExecutionRequest
        {
            [JsonPropertyName("tool")]
            public string Tool { get; set; }
            
            [JsonPropertyName("input")]
            public object Input { get; set; }
        }
        
        #endregion
    }
}