using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
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
using IDebugLogger = AIAgentTest.Services.Interfaces.IDebugLogger;

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
        }
        
        // Check connection status
        public bool IsConnected => 
            _serverProcess != null && 
            !_serverProcess.HasExited && 
            _stdinWriter != null && 
            _serverProcess.StandardInput != null && 
            _serverProcess.StandardInput.BaseStream != null;

        /// <summary>
        /// Start the server process directly without using cmd.exe,
        /// first finding the full path to the executable.
        /// </summary>
        public async Task<bool> StartServerAsync()
        {
            if (IsConnected)
            {
                _logger?.Log("[Stdio] StartServerAsync called, but server appears to be already connected.");
                return true;
            }
            if (_serverProcess != null)
            {
                _logger?.Log("[Stdio] Warning: Found existing _serverProcess instance before starting. Attempting cleanup.");
                await StopServerAsync();
            }

            // Combine external token with a timeout for the entire startup process
            using var overallTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // e.g., 30 seconds total timeout
            CancellationToken cancellationToken = default;
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, overallTimeoutCts.Token);
            var effectiveToken = linkedCts.Token;

            try
            {
                // --- Find Full Path to Executable ---
                // On Windows, npx is usually npx.cmd. Use the _command field passed in,
                // but default to "npx.cmd" on Windows if just "npx" was provided.
                string executableToFind = _command;
                if (OperatingSystem.IsWindows() &&
                    !_command.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) &&
                    !_command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) // Basic check
                {
                    executableToFind = _command + ".cmd"; // Assume .cmd for npx on Windows
                    _logger?.Log($"[Stdio] Adjusted command to search for '{executableToFind}' on Windows.");
                }

                string? fullPathToExecutable = FindExecutableInPath(executableToFind);

                if (string.IsNullOrEmpty(fullPathToExecutable))
                {
                    _logger?.Log($"[Stdio] Error: Could not find executable '{executableToFind}' in PATH environment variable.");
                    return false; // Cannot proceed without the executable path
                }
                _logger?.Log($"[Stdio] Found executable at: '{fullPathToExecutable}'");
                // --- End Find Full Path ---

                // Determine effective working directory
                string effectiveWorkingDirectory = _workingDirectory ?? string.Empty;
                _logger?.Log($"[Stdio] Effective working directory for process: '{(string.IsNullOrEmpty(effectiveWorkingDirectory) ? "[Inherited]" : effectiveWorkingDirectory)}'");
                // ** Note on 'C:/' mentioned in your error **
                // If _workingDirectory was null/empty, it inherits. If your C# app runs from C:\, that's used.
                // If _workingDirectory was explicitly set to "C:/", that's used. Running from C:\ root might cause
                // permission issues later, even if the process starts. Consider if a different working directory is more appropriate.


                var startInfo = new ProcessStartInfo
                {
                    // *** Use the full path found ***
                    FileName = fullPathToExecutable,

                    Arguments = string.Join(" ", _arguments.Select(arg => QuoteArgumentIfNeeded(arg))),
                    WorkingDirectory = effectiveWorkingDirectory,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };

                _logger?.Log($"[Stdio] Starting process directly: '{startInfo.FileName}'");
                _logger?.Log($"[Stdio] Arguments: {startInfo.Arguments}");
                // ... (Logging for working directory as before) ...

                _serverProcess = new Process { StartInfo = startInfo };
                _serverProcess.EnableRaisingEvents = true;
                _serverProcess.OutputDataReceived += OnOutputDataReceived;
                _serverProcess.ErrorDataReceived += OnErrorDataReceived;
                _serverProcess.Exited += OnProcessExited;

                if (!_serverProcess.Start())
                {
                    _logger?.Log("[Stdio] Process.Start() returned false. Failed to start process.");
                    _serverProcess.Dispose();
                    _serverProcess = null;
                    return false;
                }

                _logger?.Log($"[Stdio] Process started successfully (PID: {_serverProcess.Id}). Initializing streams...");

                // Initialize streams (added slight delay and CanWrite check)
                await Task.Delay(100);
                if (_serverProcess != null && !_serverProcess.HasExited &&
                    _serverProcess.StandardInput != null && _serverProcess.StandardInput.BaseStream != null &&
                    _serverProcess.StandardInput.BaseStream.CanWrite)
                {
                    _stdinWriter = new StreamWriter(_serverProcess.StandardInput.BaseStream, Encoding.UTF8)
                    {
                        AutoFlush = true,
                        NewLine = "\n" // Use \n for Node.js
                    };
                    _logger?.Log("[Stdio] StandardInput writer initialized.");
                }
                else
                {
                    _logger?.Log("[Stdio] Error: StandardInput stream is null, not writable, or process exited after stream check.");
                    await StopServerAsync();
                    return false;
                }

                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();

                _logger?.Log("[Stdio] Began reading stdout/stderr. Waiting for server readiness...");

                // --- ACTIVE READINESS CHECK ---
                int maxRetries = 10; // Max attempts for readiness check
                int retryDelayMs = 500; // Delay between retries
                bool isReady = false;

                for (int i = 0; i < maxRetries; i++)
                {
                    effectiveToken.ThrowIfCancellationRequested(); // Check for cancellation/timeout

                    if (_serverProcess == null || _serverProcess.HasExited)
                    {
                        _logger?.Log("[Stdio] Server process exited during readiness check loop.");
                        isReady = false;
                        break; // Exit loop
                    }

                    _logger?.Log($"[Stdio] Readiness check attempt {i + 1}/{maxRetries}: Sending 'tools/list'...");

                    // Use SendRequestAsync internally for the check, but with a shorter timeout
                    using var attemptTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)); // 5-second timeout per attempt
                    using var linkedAttemptCts = CancellationTokenSource.CreateLinkedTokenSource(effectiveToken, attemptTimeoutCts.Token);
                    try
                    {
                        

                        // Send tools/list. We don't deeply care about the result type here, just if it succeeds without RPC error.
                        // Use object or JsonElement as TResponse.
                        await SendRequestAsync<JsonElement>("tools/list", null, linkedAttemptCts.Token);

                        // If SendRequestAsync completes without throwing an exception (esp. McpErrorException for RPC errors), we're ready
                        _logger?.Log("[Stdio] Readiness check successful ('tools/list' responded).");
                        isReady = true;
                        break; // Exit loop, server is ready
                    }
                    // --- Catch specific JSON-RPC errors ---
                    // NOTE: This requires SendRequestAsync/OnOutputDataReceived to throw McpErrorException
                    //catch (McpErrorException mcpEx)
                    //{
                    //    _logger?.Log($"[Stdio] Readiness check failed (MCP Error {mcpEx.RpcError.Code}): {mcpEx.RpcError.Message}. Retrying after delay...");
                    //    // Optionally check for specific codes if needed, but any RPC error likely means not ready
                    //}
                    // --- Catch timeouts for this specific attempt ---
                    catch (OperationCanceledException) when (attemptTimeoutCts.IsCancellationRequested) // Check if it was OUR timeout
                    {
                        _logger?.Log("[Stdio] Readiness check attempt timed out. Retrying after delay...");
                    }
                    // --- Catch other potential communication errors ---
                    catch (IOException ioEx) // e.g., pipe broken if process died between checks
                    {
                        _logger?.Log($"[Stdio] Readiness check failed (IO Error): {ioEx.Message}. Retrying after delay...");
                    }
                    catch (InvalidOperationException invOpEx) // e.g., stream closed
                    {
                        _logger?.Log($"[Stdio] Readiness check failed (Invalid Operation): {invOpEx.Message}. Retrying after delay...");
                    }
                    // --- Catch unexpected errors during readiness check ---
                    catch (Exception ex)
                    {
                        _logger?.Log($"[Stdio] Readiness check failed (Unexpected Error): {ex.Message}. Retrying after delay...");
                        // Consider breaking loop on unexpected errors? Or log and retry?
                    }

                    // Wait before next retry, respecting cancellation
                    await Task.Delay(retryDelayMs, effectiveToken);
                }
                // --- END ACTIVE READINESS CHECK ---


                if (!isReady)
                {
                    _logger?.Log("[Stdio] Server failed readiness check after multiple retries or process exited.");
                    await StopServerAsync(); // Clean up the failed server
                    return false;
                }

                if (_serverProcess == null || _serverProcess.HasExited)
                {
                    _logger?.Log("[Stdio] Server process exited prematurely during readiness wait.");
                    _isStarted = false;
                    _stdinWriter?.Dispose();
                    _stdinWriter = null;
                    return false;
                }

                _isStarted = true;
                _logger?.Log("[Stdio] MCP server assumed ready after delay.");
                return true;
            }
            catch (Win32Exception w32Ex) // Catch specific process start errors
            {
                _logger?.Log($"[Stdio] Error starting process (Win32Exception): {w32Ex.Message} (Code: {w32Ex.NativeErrorCode})");
                //_logger?.Log($"[Stdio] FileName='{w32Ex.Source}', WorkingDirectory='{effectiveWorkingDirectory}'"); // Log context
                _logger?.Log($"[Stdio] Stack trace: {w32Ex.StackTrace}");
                await StopServerAsync();
                return false;
            }
            catch (InvalidOperationException ioEx)
            {
                _logger?.Log($"[Stdio] Error starting or interacting with process (InvalidOperationException): {ioEx.Message}");
                _logger?.Log($"[Stdio] Stack trace: {ioEx.StackTrace}");
                await StopServerAsync();
                return false;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[Stdio] Unexpected error starting server: {ex.Message}");
                _logger?.Log($"[Stdio] Stack trace: {ex.StackTrace}");
                await StopServerAsync();
                return false;
            }
        }

        /// <summary>
        /// Helper method to find an executable file within directories specified in the PATH environment variable.
        /// </summary>
        /// <param name="executableName">The name of the executable (e.g., "npx.cmd", "node.exe")</param>
        /// <returns>The full path to the executable if found; otherwise, null.</returns>
        private string? FindExecutableInPath(string executableName)
        {
            if (File.Exists(executableName)) // Check if it's already a full path or in the current dir
            {
                return Path.GetFullPath(executableName);
            }

            // Check common extensions on Windows if none provided
            var extensions = new List<string> { "" }; // Check for exact match first
            if (OperatingSystem.IsWindows() && !Path.HasExtension(executableName))
            {
                // Use PATHEXT environment variable if available, otherwise common defaults
                string pathExt = Environment.GetEnvironmentVariable("PATHEXT");
                if (!string.IsNullOrEmpty(pathExt))
                {
                    extensions.AddRange(pathExt.Split(';').Where(e => e.Trim().Length > 0));
                }
                else
                {
                    // Fallback extensions if PATHEXT is missing
                    extensions.AddRange(new[] { ".COM", ".EXE", ".BAT", ".CMD" });
                }
            }

            // Get PATH directories
            string? pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathVariable))
            {
                _logger?.Log("[Stdio] Warning: PATH environment variable is empty or null.");
                return null;
            }

            var pathDirectories = pathVariable.Split(Path.PathSeparator); // Handles ';' on Windows, ':' on Linux/Mac

            // Search each directory in PATH
            foreach (string pathDir in pathDirectories)
            {
                string trimmedPathDir = pathDir.Trim();
                if (string.IsNullOrEmpty(trimmedPathDir)) continue;

                // Check for the executable with each potential extension
                foreach (string extension in extensions)
                {
                    string potentialPath = Path.Combine(trimmedPathDir, executableName + extension);
                    try
                    {
                        if (File.Exists(potentialPath))
                        {
                            _logger?.Log($"[Stdio] Found '{executableName}' candidate at '{potentialPath}'");
                            // Optionally add execute permission check on Linux/Mac if needed
                            return potentialPath; // Found it
                        }
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
                    {
                        // Ignore errors checking specific paths (e.g., permissions, invalid chars)
                        _logger?.Log($"[Stdio] Debug: Ignored error checking path '{potentialPath}': {ex.Message}");
                    }
                }
            }

            _logger?.Log($"[Stdio] Executable '{executableName}' not found in any PATH directories.");
            return null; // Not found
        }


        // --- Keep the existing QuoteArgumentIfNeeded helper method ---
        /// <summary>
        /// Helper method to quote an argument if it contains spaces.
        /// </summary>
        private static string QuoteArgumentIfNeeded(string argument)
        {
            if (string.IsNullOrEmpty(argument)) return "\"\"";
            if (argument.Contains(' ') || argument.Contains('\"')) return $"\"{argument.Replace("\"", "\\\"")}\""; // Basic escaping + quoting
            return argument;
        }

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
            _logger?.Log("[Stdio] StopServerAsync called");
            
            // First, clear any pending requests
            foreach (var kvp in _pendingRequests)
            {
                try
                {
                    kvp.Value.TrySetCanceled();
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[Stdio] Error canceling pending request: {ex.Message}");
                }
            }
            _pendingRequests.Clear();

            // Then try to close the writer
            if (_stdinWriter != null)
            {
                try 
                { 
                    _stdinWriter.Close(); 
                    _stdinWriter = null;
                } 
                catch (Exception ex) 
                { 
                    _logger?.Log($"[Stdio] Error closing stdin writer: {ex.Message}"); 
                }
            }

            // Finally, kill the process if it's still running
            if (_serverProcess != null)
            {
                try
                {
                    if (!_serverProcess.HasExited)
                    {
                        _logger?.Log("[Stdio] Killing server process");
                        _serverProcess.Kill(entireProcessTree: true);
                        
                        try
                        {
                            // Create a cancellation token that cancels after 5 seconds
                            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                            await _serverProcess.WaitForExitAsync(cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            _logger?.Log("[Stdio] Timed out waiting for process to exit");
                        }
                    }
                    else
                    {
                        _logger?.Log("[Stdio] Process already exited");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[Stdio] Error killing process: {ex.Message}");
                }
                finally
                {
                    try
                    {
                        _serverProcess.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger?.Log($"[Stdio] Error disposing process: {ex.Message}");
                    }
                    _serverProcess = null;
                }
            }

            _isStarted = false;
            _logger?.Log("[Stdio] Server stopped");
        }

        /// <summary>
        /// Implements IMCPServerClient.StopServer
        /// </summary>
        public void StopServer()
        {
            // Don't block - use fire-and-forget with logging
            Task.Run(async () => {
                try
                {
                    _logger?.Log("[Stdio] StopServer called, stopping server in background...");
                    await StopServerAsync();
                    _logger?.Log("[Stdio] StopServer completed successfully");
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[Stdio] Error in StopServer background task: {ex.Message}");
                }
            });
            
            // Set these immediately rather than waiting
            _isStarted = false;
            
            _logger?.Log("[Stdio] StopServer has initiated server shutdown in background");
        }
        
        /// <summary>
        /// Extended version of GetToolsAsync with option to start server
        /// </summary>
        /// <param name="startServerIfNeeded">Whether to start the server if it's not already running</param>
        /// <param name="clearCache">Whether to clear the cached tools and try to get fresh ones from the server</param>
        /// <returns>List of tool definitions</returns>
        private async Task<List<ToolDefinition>> GetToolsWithStartOption(bool startServerIfNeeded = true, bool clearCache = false)
        {
            _logger?.Log($"[Stdio] GetToolsWithStartOption called with startServerIfNeeded={startServerIfNeeded}, clearCache={clearCache}");
            
            // Clear cache if requested
            if (clearCache && _cachedTools != null)
            {
                _logger?.Log("[Stdio] Clearing cached tools as requested");
                _cachedTools = null;
            }
            
            // Return cached tools if available and not clearing cache
            if (!clearCache && _cachedTools != null && _cachedTools.Count > 0)
            {
                _logger?.Log($"[Stdio] Returning {_cachedTools.Count} cached tools");
                return _cachedTools;
            }
            
            // Only try to start the server if requested and we're not already started
            if (!_isStarted && startServerIfNeeded)
            {
                _logger?.Log("[Stdio] Server not started, attempting to start...");
                bool started = false;
                try
                {
                    started = await StartServerAsync();
                    _logger?.Log($"[Stdio] StartServerAsync result: {started}");
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[Stdio] Error starting server during GetToolsWithStartOption: {ex.Message}");
                    started = false;
                }
                
                if (!started)
                {
                    _logger?.Log("[Stdio] Failed to start server, falling back to hardcoded tools");
                    _cachedTools = GetHardcodedTools();
                    return _cachedTools;
                }
            }
            
            // At this point, we should have a running server or we couldn't start it
            // Let's try to communicate with it to get the tools
            try
            {
                _logger?.Log("[Stdio] Attempting to discover tools from server");
                
                try
                {
                    // Make sure we're really connected before proceeding
                    if (!IsConnected)
                    {
                        _logger?.Log("[Stdio] Server is not connected, cannot discover tools.");
                        //_logger?.Log("[Stdio] Server is not connected, cannot discover tools. Falling back to hardcoded tools.");
                        //_cachedTools = GetHardcodedTools();
                        return _cachedTools;
                    }
                    
                    // Wait a bit longer for the server to be ready for requests
                    _logger?.Log("[Stdio] Waiting for server to be ready for requests...");
                    await Task.Delay(10);
                    //await Task.Delay(1000);

                    // Send request for tools listing
                    _logger?.Log("[Stdio] Sending tools/list request to stdio server");
                    var result = await SendRequestAsync<JsonElement>("tools/list");
                    
                    _logger?.Log($"[Stdio] Received response from tools/list: {result.GetRawText()}");

                    var test = result.GetRawText();

                    if (result.TryGetProperty("tools", out var toolsElement))
                    {
                        int toolCount = toolsElement.GetArrayLength();
                        _logger?.Log($"[Stdio] Successfully retrieved {toolCount} tools from server");
                        
                        // Only accept empty tools list if we successfully communicated with the server
                        if (toolCount == 0)
                        {
                            _logger?.Log("[Stdio] Server returned empty tools list, using empty list");
                            _cachedTools = new List<ToolDefinition>();
                            return _cachedTools;
                        }
                        
                        // Deserialize the tools from the response
                        var discoveredTools = JsonSerializer.Deserialize<List<ToolDefinition>>(toolsElement.GetRawText(), _jsonOptions);

                        foreach (var tool in discoveredTools)
                        {
                            // Process the raw input schema to populate the compatibility fields
                            if(tool.RawInputSchema.ValueKind == JsonValueKind.Object)
                                tool.ProcessRawInputSchema();
                        }

                        // Enhance the tools with proper metadata
                        foreach (var tool in discoveredTools)
                        {
                            // Add or update metadata
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
                        
                        _logger?.Log($"[Stdio] Successfully processed {discoveredTools.Count} tools from server");
                        _cachedTools = discoveredTools;
                        return _cachedTools;
                    }
                    else
                    {
                        _logger?.Log("[Stdio] Tools property not found in response, using hardcoded tools");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[Stdio] Error getting tools via JSON-RPC: {ex.Message}");
                    _logger?.Log($"[Stdio] Stack trace: {ex.StackTrace}");
                }
            }
            catch (Exception ex)
            {
                _logger?.Log($"[Stdio] Error in GetToolsWithStartOption: {ex.Message}");
            }
            
            // If we get here, something went wrong with the server communication
            // Fall back to hardcoded tools - lets not do this anyway.
            //_logger?.Log("[Stdio] All attempts to get tools from server failed, using hardcoded tools as last resort");
            //_cachedTools = GetHardcodedTools();


            return _cachedTools;
        }
        
        /// <summary>
        /// IMCPServerClient implementation of GetToolsAsync
        /// </summary>
        public async Task<List<ToolDefinition>> GetToolsAsync()
        {
            _logger?.Log("[Stdio] IMCPServerClient.GetToolsAsync() called");
            
            // We're explicitly bypassing the cache here to ensure we talk to the server
            _logger?.Log("[Stdio] Calling GetToolsWithStartOption with clearCache=true to force server communication");
            return await GetToolsWithStartOption(true, true);
        }
        
        /// <summary>
        /// Execute a tool on the server
        /// </summary>
        public async Task<object> ExecuteToolAsync(string toolName, object input)
        {
            _logger?.Log($"[Stdio] ExecuteToolAsync called for tool: {toolName}");
            
            if (string.IsNullOrEmpty(toolName))
                throw new ArgumentNullException(nameof(toolName));
            
            // Make sure the server is started
            if (!_isStarted)
            {
                _logger?.Log("[Stdio] Server not started, attempting to start...");
                try
                {
                    bool started = await StartServerAsync();
                    if (!started)
                    {
                        _logger?.Log("[Stdio] Failed to start server for tool execution");
                        return new { error = "MCP server could not be started" };
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[Stdio] Error starting server for tool execution: {ex.Message}");
                    return new { error = $"Failed to start MCP server: {ex.Message}" };
                }
            }
            
            // Make sure we're connected
            if (!IsConnected)
            {
                _logger?.Log("[Stdio] Server is not connected, attempting reconnection...");
                try
                {
                    bool started = await StartServerAsync();
                    if (!started || !IsConnected)
                    {
                        _logger?.Log("[Stdio] Failed to reconnect to server");
                        return new { error = "Server connection could not be established" };
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Log($"[Stdio] Error reconnecting to server: {ex.Message}");
                    return new { error = $"Failed to reconnect to server: {ex.Message}" };
                }
            }
            
            // Now try to execute the tool
            try
            {
                _logger?.Log($"[Stdio] Executing tool '{toolName}' with input: {JsonSerializer.Serialize(input)}");
                
                // Validate input format
                var validatedInput = PrepareToolInput(toolName, input);
                if (validatedInput is Dictionary<string, object> error && error.ContainsKey("error"))
                {
                    return error;
                }
                
                // Send the request to the server
                var response = await SendRequestAsync<JsonElement>(toolName, validatedInput);
                _logger?.Log($"[Stdio] Tool execution response: {response.GetRawText()}");
                
                return JsonSerializer.Deserialize<object>(response.GetRawText(), _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger?.Log($"[Stdio] Error executing tool '{toolName}': {ex.GetType().Name}: {ex.Message}");
                _logger?.Log($"[Stdio] Stack trace: {ex.StackTrace}");

                //_logger?.Log($"[Stdio] Error executing tool '{toolName}': {ex.Message}");
                //_logger?.Log($"[Stdio] Exception details: {ex}");
                return new { error = $"Error executing tool: {ex.Message}" };
            }
        }
        
        /// <summary>
        /// Prepares and validates tool input
        /// </summary>
        private object PrepareToolInput(string toolName, object input)
        {
            try
            {
                // If input is already a dictionary, use it directly
                if (input is Dictionary<string, object> dictInput)
                {
                    return dictInput;
                }
                
                // If input is a JsonElement, deserialize it
                if (input is JsonElement jsonElement)
                {
                    var deserialized = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonElement.GetRawText());
                    return deserialized;
                }
                
                // Otherwise serialize and deserialize to ensure proper format
                string jsonString = JsonSerializer.Serialize(input);
                var result = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
                return result;
            }
            catch (Exception ex)
            {
                _logger?.Log($"[Stdio] Error preparing input for tool '{toolName}': {ex.Message}");
                return new Dictionary<string, object> { { "error", $"Invalid input format: {ex.Message}" } };
            }
        }
        
        /// <summary>
        /// Check if the server is available
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            if (_isStarted && IsConnected)
                return true;
                
            return await StartServerAsync();
        }
        
        /// <summary>
        /// Send a request to the server
        /// </summary>
        private async Task<TResponse> SendRequestAsync<TResponse>(string method, object parameters = null, CancellationToken cancellationToken = default)
        {
            if (!IsConnected) 
            {
                try
                {
                    bool started = await StartServerAsync();
                    if (!started)
                    {
                        throw new InvalidOperationException("Could not start server.");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Log($"Error starting server in SendRequestAsync: {ex.Message}");
                    throw new InvalidOperationException("Failed to start server.", ex);
                }
                
                // Double-check connection after start attempt
                if (!IsConnected)
                {
                    throw new InvalidOperationException("Server connection could not be established.");
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
                if (_stdinWriter == null)
                {
                    _logger?.Log("Error: _stdinWriter is null when attempting to send request");
                    throw new InvalidOperationException("Server I/O stream not available.");
                }
                
                string jsonRequest = JsonSerializer.Serialize(request, _jsonOptions);
                _logger?.Log($"Sending JSON-RPC request: {jsonRequest}");
                //_stdinWriter.NewLine = "\n";
                await _stdinWriter.WriteLineAsync(jsonRequest.AsMemory(), cancellationToken);
                //await _stdinWriter.WriteLineAsync(jsonRequest.AsMemory(), cancellationToken);
                await _stdinWriter.FlushAsync();
                await Task.Delay(50);

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
                //if(ex.Message.Contains("32601"))
                //{
                //    await Task.Delay(1000); // Wait 1 second
                //    try
                //    {
                //        string jsonRequest = JsonSerializer.Serialize(request, _jsonOptions);
                //        // Prepare input again or reuse...
                //        var result = await toolHandler(input);
                //        // process result...
                //    }
                //    catch (Exception retryEx)
                //    {
                //        _logger?.Log($"[Stdio] Retry failed: {retryEx.Message}");
                //        // Handle final failure... return error object, throw, etc.
                //    }
                //}
                
                _pendingRequests.TryRemove(requestId, out _);
                _logger?.Log($"Error in SendRequestAsync: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            StopServer();
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// Async dispose
        /// </summary>
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
                }
                //,
                
                //// Tool 2: write_file
                //new ToolDefinition
                //{
                //    Name = "write_file",
                //    Description = "Create a new file or completely overwrite an existing file with new content. Use with caution as it will overwrite existing files without warning. Handles text content with proper encoding. Only works within allowed directories.",
                //    Input = new Dictionary<string, object>
                //    {
                //        { "type", "object" },
                //        { "properties", new Dictionary<string, object>
                //            {
                //                { "path", new Dictionary<string, string>
                //                    {
                //                        { "type", "string" },
                //                        { "description", "Path to the file" }
                //                    }
                //                },
                //                { "content", new Dictionary<string, string>
                //                    {
                //                        { "type", "string" },
                //                        { "description", "Content to write" }
                //                    }
                //                }
                //            }
                //        },
                //        { "required", new[] { "path", "content" } }
                //    },
                //    Tags = new[] { "Filesystem", "MCP" },
                //    Metadata = new Dictionary<string, object>
                //    {
                //        { "server_name", "FileServer" },
                //        { "server_type", "filesystem" }
                //    }
                //},
                
                //// Tool 3: list_directory
                //new ToolDefinition
                //{
                //    Name = "list_directory",
                //    Description = "Get a detailed listing of all files and directories in a specified path. Results clearly distinguish between files and directories with [FILE] and [DIR] prefixes. This tool is essential for understanding directory structure and finding specific files within a directory. Only works within allowed directories.",
                //    Input = new Dictionary<string, object>
                //    {
                //        { "type", "object" },
                //        { "properties", new Dictionary<string, object>
                //            {
                //                { "path", new Dictionary<string, string>
                //                    {
                //                        { "type", "string" },
                //                        { "description", "Path to the directory" }
                //                    }
                //                }
                //            }
                //        },
                //        { "required", new[] { "path" } }
                //    },
                //    Tags = new[] { "Filesystem", "MCP" },
                //    Metadata = new Dictionary<string, object>
                //    {
                //        { "server_name", "FileServer" },
                //        { "server_type", "filesystem" }
                //    }
                //},
                
                //// Tool 4: directory_tree
                //new ToolDefinition
                //{
                //    Name = "directory_tree",
                //    Description = "Get a recursive tree view of files and directories as a JSON structure. Each entry includes 'name', 'type' (file/directory), and 'children' for directories. Files have no children array, while directories always have a children array (which may be empty). The output is formatted with 2-space indentation for readability. Only works within allowed directories.",
                //    Input = new Dictionary<string, object>
                //    {
                //        { "type", "object" },
                //        { "properties", new Dictionary<string, object>
                //            {
                //                { "path", new Dictionary<string, string>
                //                    {
                //                        { "type", "string" },
                //                        { "description", "Path to the directory" }
                //                    }
                //                }
                //            }
                //        },
                //        { "required", new[] { "path" } }
                //    },
                //    Tags = new[] { "Filesystem", "MCP" },
                //    Metadata = new Dictionary<string, object>
                //    {
                //        { "server_name", "FileServer" },
                //        { "server_type", "filesystem" }
                //    }
                //},
                
                //// Tool 5: list_allowed_directories
                //new ToolDefinition
                //{
                //    Name = "list_allowed_directories",
                //    Description = "Returns the list of directories that this server is allowed to access. Use this to understand which directories are available before trying to access files.",
                //    Input = new Dictionary<string, object>
                //    {
                //        { "type", "object" },
                //        { "properties", new Dictionary<string, object>() },
                //        { "required", new string[] { } }
                //    },
                //    Tags = new[] { "Filesystem", "MCP" },
                //    Metadata = new Dictionary<string, object>
                //    {
                //        { "server_name", "FileServer" },
                //        { "server_type", "filesystem" }
                //    }
                //}
            };
        }
    }
}