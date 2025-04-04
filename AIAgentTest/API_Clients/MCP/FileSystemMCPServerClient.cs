using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AIAgentTest.Services.MCP;
using MCPParameterDefinition = AIAgentTest.Services.MCP.ParameterDefinition;

namespace AIAgentTest.API_Clients.MCP
{
    /// <summary>
    /// MCP server client that uses the ModelContextProtocol.io filesystem server implementation
    /// </summary>
    public class FileSystemMCPServerClient : IMCPServerClient
    {
        private readonly string _command;
        private readonly string[] _args;
        private Process _serverProcess;
        private bool _isStarted = false;
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="command">Command to run (e.g., "npx")</param>
        /// <param name="args">Arguments for the command (e.g., ["-y", "@modelcontextprotocol/server-filesystem", "C:\\path"])</param>
        public FileSystemMCPServerClient(string command, string[] args)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _args = args ?? throw new ArgumentNullException(nameof(args));
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
                _serverProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _command,
                        Arguments = string.Join(" ", _args),
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                _serverProcess.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                        Console.WriteLine($"MCP Server: {e.Data}");
                };
                
                _serverProcess.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                        Console.WriteLine($"MCP Server Error: {e.Data}");
                };
                
                _serverProcess.Start();
                _serverProcess.BeginOutputReadLine();
                _serverProcess.BeginErrorReadLine();
                
                // Wait a bit for the server to start
                await Task.Delay(2000);
                
                if (_serverProcess.HasExited)
                    return false;
                    
                _isStarted = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start MCP server: {ex.Message}");
                return false;
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
                    _serverProcess.Kill();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error stopping MCP server: {ex.Message}");
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
                    return new List<ToolDefinition>();
            }
            
            // In a real implementation, we would make a proper API call to the MCP server
            // For now, we'll return some example tools that represent filesystem capabilities
            var tools = new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "read_file",
                    Description = "Reads a file from the file system",
                    Parameters = new Dictionary<string, MCPParameterDefinition>
                    {
                        { "path", new MCPParameterDefinition 
                          { 
                              Type = "string", 
                              Description = "Path to the file", 
                              Required = true 
                          } 
                        }
                    },
                    Tags = new[] { "Filesystem", "MCP" }
                },
                new ToolDefinition
                {
                    Name = "list_directory",
                    Description = "Lists files and directories in a specified path",
                    Parameters = new Dictionary<string, MCPParameterDefinition>
                    {
                        { "path", new MCPParameterDefinition 
                          { 
                              Type = "string", 
                              Description = "Path to the directory", 
                              Required = true 
                          } 
                        }
                    },
                    Tags = new[] { "Filesystem", "MCP" }
                },
                new ToolDefinition
                {
                    Name = "write_file",
                    Description = "Writes content to a file",
                    Parameters = new Dictionary<string, MCPParameterDefinition>
                    {
                        { "path", new MCPParameterDefinition 
                          { 
                              Type = "string", 
                              Description = "Path to the file", 
                              Required = true 
                          } 
                        },
                        { "content", new MCPParameterDefinition 
                          { 
                              Type = "string", 
                              Description = "Content to write", 
                              Required = true 
                          } 
                        }
                    },
                    Tags = new[] { "Filesystem", "MCP" }
                }
            };
            
            return tools;
        }
        
        /// <summary>
        /// Execute a tool on the server
        /// </summary>
        /// <param name="toolName">Tool name</param>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        public async Task<object> ExecuteToolAsync(string toolName, object input)
        {
            if (!_isStarted)
            {
                if (!await StartServerAsync())
                    return new { error = "MCP server is not running" };
            }
            
            try
            {
                // In a real implementation, we would make a proper API call to the MCP server
                // For now, we'll simulate basic functionality of filesystem tools
                switch (toolName)
                {
                    case "read_file":
                        var path = JsonSerializer.Deserialize<Dictionary<string, string>>(input.ToString())["path"];
                        if (File.Exists(path))
                        {
                            var content = await File.ReadAllTextAsync(path);
                            return new { content };
                        }
                        return new { error = $"File not found: {path}" };
                        
                    case "list_directory":
                        var dirPath = JsonSerializer.Deserialize<Dictionary<string, string>>(input.ToString())["path"];
                        if (Directory.Exists(dirPath))
                        {
                            var files = Directory.GetFiles(dirPath).Select(Path.GetFileName).ToArray();
                            var dirs = Directory.GetDirectories(dirPath).Select(Path.GetFileName).ToArray();
                            return new { files, directories = dirs };
                        }
                        return new { error = $"Directory not found: {dirPath}" };
                        
                    case "write_file":
                        var writeParams = JsonSerializer.Deserialize<Dictionary<string, string>>(input.ToString());
                        var writePath = writeParams["path"];
                        var fileContent = writeParams["content"];
                        try
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(writePath));
                            await File.WriteAllTextAsync(writePath, fileContent);
                            return new { success = true, message = $"File written to {writePath}" };
                        }
                        catch (Exception ex)
                        {
                            return new { error = $"Failed to write file: {ex.Message}" };
                        }
                        
                    default:
                        return new { error = $"Tool '{toolName}' not implemented" };
                }
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        }
        
        /// <summary>
        /// Check if the server is available
        /// </summary>
        /// <returns>True if available</returns>
        public async Task<bool> IsAvailableAsync()
        {
            if (_isStarted && _serverProcess != null && !_serverProcess.HasExited)
                return true;
                
            return await StartServerAsync();
        }
        
        /// <summary>
        /// Clean up resources when disposed
        /// </summary>
        public void Dispose()
        {
            StopServer();
        }
    }
}
