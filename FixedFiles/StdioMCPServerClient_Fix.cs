using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
    /// Implementation of IMCPServerClient that communicates with MCP servers via standard I/O
    /// </summary>
    public class StdioMCPServerClient : IMCPServerClient, IAsyncDisposable
    {
        private readonly string _command;
        private readonly string[] _arguments;
        private readonly string _workingDirectory;
        private readonly IDebugLogger _logger;
        private Process _serverProcess;
        private StreamWriter _stdinWriter;
        private long _requestIdCounter = 0;
        private bool _isStarted = false;
        private List<ToolDefinition> _cachedTools;
        
        // Stores pending requests
        private readonly ConcurrentDictionary<object, TaskCompletionSource<JsonElement>> _pendingRequests = 
            new ConcurrentDictionary<object, TaskCompletionSource<JsonElement>>();
        
        // JSON options
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        
        // Constructor
        public StdioMCPServerClient(string command, string[] arguments, string workingDirectory = null, IDebugLogger logger = null)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
            _workingDirectory = workingDirectory;
            _logger = logger ?? ServiceProvider.GetService<IDebugLogger>();
            
            // Log the arguments
            _logger?.Log($"StdioMCPServerClient created with command: {command} and args: {string.Join(" ", arguments)}");
        }
        
        // Check connection status
        public bool IsConnected => _serverProcess != null && !_serverProcess.HasExited && _stdinWriter != null;
        
        // Start the server
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
        
        // Rest of the implementation stays the same...
        
        // Handle process exit
        private void OnProcessExited(object sender, EventArgs e)
        {
            foreach (var tcs in _pendingRequests.Values)
            {
                tcs.TrySetException(new IOException("MCP server process exited unexpectedly."));
            }
            _pendingRequests.Clear();
            _serverProcess = null;
            _stdinWriter = null;
            _isStarted = false;
        }

        // Handle error output
        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data))
                return;
            
            // Always log the data
            _logger?.Log($"[Stdio] {e.Data}");
            
            // Don't treat informational messages as errors
            if (e.Data.Contains("running on stdio") || e.Data.Contains("Allowed directories"))
            {
                _logger?.Log($"[Stdio] Server INFO: {e.Data}");
            }
            else
            {
                _logger?.Log($"[Stdio] Server STDERR: {e.Data}");
            }
        }

        // Handle standard output
        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            var line = e.Data;
            if (string.IsNullOrEmpty(line))
                return;
            
            // Always log the raw output
            _logger?.Log($"[Stdio] STDOUT: {line}");

            try
            {
                using var jsonDoc = JsonDocument.Parse(line);
                _logger?.Log($"[Stdio] Parsed valid JSON: {line}");

                if (jsonDoc.RootElement.TryGetProperty("id", out var idElement))
                {
                    object id = idElement.ValueKind switch {
                        JsonValueKind.Number => idElement.GetInt64(),
                        JsonValueKind.String => idElement.GetString(),
                        _ => throw new JsonException("Invalid ID type")
                    };

                    _logger?.Log($"[Stdio] JSON-RPC response with ID: {id}");

                    if (_pendingRequests.TryRemove(id, out var tcs))
                    {
                        if (jsonDoc.RootElement.TryGetProperty("error", out var errorElement))
                        {
                            _logger?.Log($"[Stdio] Error in JSON-RPC response: {errorElement}");
                            tcs.TrySetException(new Exception($"RPC Error: {errorElement}"));
                        }
                        else if (jsonDoc.RootElement.TryGetProperty("result", out var resultElement))
                        {
                            _logger?.Log($"[Stdio] Received result for request ID {id}");
                            tcs.TrySetResult(resultElement.Clone());
                        }
                        else
                        {
                            _logger?.Log($"[Stdio] Invalid JSON-RPC response (no error or result): {line}");
                            tcs.TrySetException(new JsonException("Invalid JSON-RPC response"));
                        }
                    }
                    else
                    {
                        _logger?.Log($"[Stdio] Received response for unknown request ID: {id}");
                    }
                }
                else
                {
                    _logger?.Log($"[Stdio] JSON message without ID property: {line}");
                }
            }
            catch (JsonException jsonEx)
            {
                _logger?.Log($"[Stdio] Not valid JSON: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                _logger?.Log($"[Stdio] Error processing output: {ex.Message}");
            }
        }

        // Stop the server
        public async Task StopServerAsync()
        {
            if (_serverProcess == null) return;

            foreach (var kvp in _pendingRequests)
            {
                kvp.Value.TrySetCanceled();
            }
            _pendingRequests.Clear();

            try { _stdinWriter?.Close(); } catch { }

            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                try
                {
                    _serverProcess.Kill(entireProcessTree: true);
                    
                    // Create a cancellation token that cancels after 5 seconds
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await _serverProcess.WaitForExitAsync(cts.Token);
                }
                catch (Exception ex)
                {
                    _logger?.Log($"Error killing process: {ex.Message}");
                }
            }

            _serverProcess?.Dispose();
            _serverProcess = null;
            _stdinWriter = null;
            _isStarted = false;
        }

        // IMCPServerClient implementation
        public void StopServer()
        {
            Task.Run(StopServerAsync).GetAwaiter().GetResult();
        }
        
        // Get available tools
        public async Task<List<ToolDefinition>> GetToolsAsync()
        {
            if (!_isStarted)
            {
                if (!await StartServerAsync())
                {
                    _logger?.Log("Failed to start server, using hardcoded tools");
                    _cachedTools = GetHardcodedTools();
                    return _cachedTools;
                }
            }
            
            if (_cachedTools != null && _cachedTools.Count > 0)
            {
                return _cachedTools;
            }
            
            try
            {
                _logger?.Log("Server running in stdio mode, sending tools/list request");
                
                try
                {
                    // Wait a bit longer for the server to be ready for requests
                    await Task.Delay(2000);
                    
                    // Send request for tools listing
                    _logger?.Log("[Stdio] Sending tools/list request to stdio server");
                    var result = await SendRequestAsync<JsonElement>("tools/list");
                    
                    _logger?.Log($"[Stdio] Received response from tools/list: {result.GetRawText()}");
                    
                    if (result.TryGetProperty("tools", out var toolsElement))
                    {
                        int toolCount = toolsElement.GetArrayLength();
                        _logger?.Log($"[Stdio] Successfully retrieved {toolCount} tools from server");
                        _cachedTools = JsonSerializer.Deserialize<List<ToolDefinition>>(toolsElement.GetRawText(), _jsonOptions);
                        return _cachedTools;
                    }
                    else
                    {
                        _logger?.Log("[Stdio] Tools property not found in response, using hardcoded tools");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Log($"Error getting tools via JSON-RPC: {ex.Message}");
                    _logger?.Log("Using hardcoded tool definitions as fallback");
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"Error in GetToolsAsync: {ex.Message}");
            }
            
            // Fallback to hardcoded tools if anything went wrong
            _logger?.Log("Using hardcoded tool definitions");
            _cachedTools = GetHardcodedTools();
            return _cachedTools;
        }
        
        // Execute a tool
        public async Task<object> ExecuteToolAsync(string toolName, object input)
        {
            if (string.IsNullOrEmpty(toolName))
                throw new ArgumentNullException(nameof(toolName));
            
            if (!_isStarted)
            {
                if (!await StartServerAsync())
                {
                    return new { error = "MCP server is not running" };
                }
            }
            
            try
            {
                var response = await SendRequestAsync<JsonElement>(toolName, input);
                return JsonSerializer.Deserialize<object>(response.GetRawText(), _jsonOptions);
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        }
        
        // Check if server is available
        public async Task<bool> IsAvailableAsync()
        {
            if (_isStarted && IsConnected)
                return true;
                
            return await StartServerAsync();
        }
        
        // Send a request to the server
        private async Task<TResponse> SendRequestAsync<TResponse>(string method, object parameters = null, CancellationToken cancellationToken = default)
        {
            if (!IsConnected) 
            {
                if (!await StartServerAsync())
                {
                    throw new InvalidOperationException("Failed to start server.");
                }
            }

            var requestId = Interlocked.Increment(ref _requestIdCounter);
            
            // Create request object
            var request = new 
            {
                jsonrpc = "2.0",
                method,
                @params = parameters,
                id = requestId
            };
            
            var tcs = new TaskCompletionSource<JsonElement>();

            if (!_pendingRequests.TryAdd(requestId, tcs))
            {
                throw new InvalidOperationException("Failed to register request.");
            }

            try
            {
                string jsonRequest = JsonSerializer.Serialize(request, _jsonOptions);
                _logger?.Log($"Sending JSON-RPC request: {jsonRequest}");
                await _stdinWriter.WriteLineAsync(jsonRequest.AsMemory(), cancellationToken);
                await _stdinWriter.FlushAsync();

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(60)); // Use a longer timeout
                
                _logger?.Log($"Waiting for response to request ID {requestId} with 60-second timeout");

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));

                if (completedTask == tcs.Task)
                {
                    var resultElement = await tcs.Task;
                    _logger?.Log($"Received JSON-RPC response for request ID {requestId}");
                    return JsonSerializer.Deserialize<TResponse>(resultElement.GetRawText(), _jsonOptions);
                }
                else
                {
                    _pendingRequests.TryRemove(requestId, out _);
                    _logger?.Log($"Timeout waiting for response to request ID {requestId}");
                    throw new OperationCanceledException("Timeout waiting for server response");
                }
            }
            catch (Exception ex)
            {
                _pendingRequests.TryRemove(requestId, out _);
                _logger?.Log($"Error in SendRequestAsync: {ex.Message}");
                throw;
            }
        }

        // Dispose resources
        public void Dispose()
        {
            StopServer();
            GC.SuppressFinalize(this);
        }
        
        // Async dispose
        public async ValueTask DisposeAsync()
        {
            await StopServerAsync();
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Provides hardcoded tool definitions as a fallback
        /// </summary>
        private List<ToolDefinition> GetHardcodedTools()
        {
            _logger?.Log("Using hardcoded tool definitions in StdioMCPServerClient");
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
                
                // Tool 2: write_file
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
                
                // Tool 3: list_directory
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
                
                // Tool 4: directory_tree
                new ToolDefinition
                {
                    Name = "directory_tree",
                    Description = "Get a recursive tree view of files and directories as a JSON structure. Each entry includes 'name', 'type' (file/directory), and 'children' for directories. Files have no children array, while directories always have a children array (which may be empty). The output is formatted with 2-space indentation for readability. Only works within allowed directories.",
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
    }
}