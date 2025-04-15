using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AIAgentTest.Common;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Common tools for MCP
    /// </summary>
    public class CommonTools
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly List<string> _allowedDirectories = new List<string>();
        private readonly ToolOperationLogger _logger;
        
        /// <summary>
        /// Create a simple success response with a message
        /// </summary>
        private Dictionary<string, object> CreateSuccessResponse(string message, string type = null)
        {
            var response = new Dictionary<string, object>
            {
                ["success"] = true,
                ["message"] = message
            };
            
            if (type != null)
            {
                response["type"] = type;
            }
            
            return response;
        }
        
        /// <summary>
        /// Create a simple error response with a message
        /// </summary>
        private Dictionary<string, object> CreateErrorResponse(string errorMessage)
        {
            return new Dictionary<string, object>
            {
                ["error"] = errorMessage
            };
        }
        
        /// <summary>
        /// Constructor
        /// </summary>
        public CommonTools(ToolOperationLogger logger = null)
        {
            _logger = logger;
            
            // Default allowed directories
            _allowedDirectories.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            _allowedDirectories.Add(Path.GetTempPath());
            _allowedDirectories.Add(AppDomain.CurrentDomain.BaseDirectory);
            
            // Add application data folder
            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                "AIAgent");
                
            // Create app data directory if it doesn't exist
            if (!Directory.Exists(appDataFolder))
            {
                try
                {
                    Directory.CreateDirectory(appDataFolder);
                    _allowedDirectories.Add(appDataFolder);
                }
                catch
                {
                    // Ignore if we can't create the directory
                }
            }
            else
            {
                _allowedDirectories.Add(appDataFolder);
            }
        }
        
        /// <summary>
        /// Add a directory to the allowed list
        /// </summary>
        public void AddAllowedDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                var fullPath = Path.GetFullPath(path);
                if (!_allowedDirectories.Contains(fullPath))
                {
                    _allowedDirectories.Add(fullPath);
                }
            }
        }

        /// <summary>
        /// Register common tools with a tool registry
        /// </summary>
        /// <param name="registry">Tool registry</param>
        public void RegisterCommonTools(IToolRegistry registry)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            
            // Register list_tools tool
            registry.RegisterTool(
                new ToolDefinition
                {
                    Name = "list_tools",
                    Description = "List available tools and their descriptions",
                    Schema = JsonSerializer.Serialize(new
                    {
                        type = "object",
                        properties = new { },
                    }),
                    Tags = new[] { "Meta", "System" },
                    Metadata = new Dictionary<string, object>
                    {
                        { "friendly_name", "List Tools" },
                        { "priority", 0 }
                    }
                },
                ListToolsHandler);
            
            // Register date/time tool
            registry.RegisterTool(
                new ToolDefinition
                {
                    Name = "get_date_time",
                    Description = "Get the current date and time",
                    Schema = JsonSerializer.Serialize(new
                    {
                        type = "object",
                        properties = new { },
                    }),
                    Tags = new[] { "Utility" },
                    Metadata = new Dictionary<string, object>
                    {
                        { "friendly_name", "Date and Time" },
                        { "priority", 1 }
                    }
                },
                GetDateTimeHandler);
            
            // Register filesystem tools
            RegisterReadFileTool(registry);
            RegisterReadMultipleFilesTool(registry);
            RegisterWriteFileTool(registry);
            RegisterEditFileTool(registry);
            RegisterCreateDirectoryTool(registry);
            RegisterListDirectoryTool(registry);
            RegisterDirectoryTreeTool(registry);
            RegisterMoveFileTool(registry);
            RegisterSearchFilesTool(registry);
            RegisterGetFileInfoTool(registry);
            RegisterListAllowedDirectoriesTool(registry);
        }
        
        /// <summary>
        /// Checks if a path is within allowed directories
        /// </summary>
        private bool IsPathAllowed(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                var fullPath = Path.GetFullPath(path);
                
                return _allowedDirectories.Any(allowedDir => 
                    fullPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Handle list_tools requests
        /// </summary>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        private async Task<object> ListToolsHandler(object input)
        {
            Console.WriteLine($"ListToolsHandler called with input: {JsonSerializer.Serialize(input)}");
            
            // Get the tool registry from the service provider
            var toolRegistry = ServiceProvider.GetService<IToolRegistry>();
            if (toolRegistry == null)
            {
                return CreateErrorResponse("Tool registry not available");
            }
            
            // Get all registered tools
            var tools = toolRegistry.GetAllTools();
            
            // Format the tools for display
            var toolsList = tools.Select(t => new Dictionary<string, object>
            {
                ["name"] = t.Name,
                ["description"] = t.Description,
                ["tags"] = t.Tags,
                ["schema"] = t.Schema
            }).ToList();
            
            var result = new Dictionary<string, object>
            {
                ["tools"] = toolsList
            };
            
            Console.WriteLine($"ListToolsHandler returning information about {tools.Count} tools");
            return result;
        }
        
        /// <summary>
        /// Handle date/time requests
        /// </summary>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        private async Task<object> GetDateTimeHandler(object input)
        {
            Console.WriteLine($"GetDateTimeHandler called with input: {JsonSerializer.Serialize(input)}");
            
            var now = DateTime.Now;
            var utcNow = DateTime.UtcNow;
            
            var result = new Dictionary<string, object>
            {
                ["localDateTime"] = now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["utcDateTime"] = utcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                ["timeZone"] = TimeZoneInfo.Local.DisplayName,
                ["unixTimestamp"] = DateTimeOffset.Now.ToUnixTimeSeconds()
            };
            
            Console.WriteLine($"GetDateTimeHandler returning: {JsonSerializer.Serialize(result)}");
            return result;
        }

        #region File System Tools
        
        /// <summary>
        /// Register a tool to read file contents
        /// </summary>
        private void RegisterReadFileTool(IToolRegistry registry)
        {
            var toolDefinition = new ToolDefinition
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
                            type = "string",
                            description = "The path to the file"
                        }
                    },
                    required = new[] { "path" },
                    additionalProperties = false
                }),
                Tags = new[] { "File System" },
                Metadata = new Dictionary<string, object>
                {
                    { "friendly_name", "Read File" },
                    { "priority", 10 }
                }
            };

            registry.RegisterTool(toolDefinition, ReadFileHandler);
        }

        /// <summary>
        /// Handle file read requests
        /// </summary>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        private async Task<object> ReadFileHandler(object input)
        {
            var options = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(input));
            
            if (!options.TryGetValue("path", out var pathObj) || pathObj == null)
            {
                return CreateErrorResponse("Path parameter is required");
            }
            
            string path = pathObj.ToString();
            
            if (!IsPathAllowed(path))
            {
                return CreateErrorResponse($"Access to path '{path}' is not allowed. Use list_allowed_directories to see available directories.");
            }
            
            try
            {
                if (!File.Exists(path))
                {
                    return CreateErrorResponse($"File not found: {path}");
                }
                
                var content = await File.ReadAllTextAsync(path);
                return new Dictionary<string, object>
                {
                    ["content"] = content
                };
            }
            catch (Exception ex)
            {
                return CreateErrorResponse($"Error reading file: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Register a tool to read multiple files simultaneously
        /// </summary>
        private void RegisterReadMultipleFilesTool(IToolRegistry registry)
        {
            var toolDefinition = new ToolDefinition
            {
                Name = "read_multiple_files",
                Description = "Read the contents of multiple files simultaneously. This is more efficient than reading files one by one when you need to analyze or compare multiple files. Each file's content is returned with its path as a reference. Failed reads for individual files won't stop the entire operation. Only works within allowed directories.",
                Schema = JsonSerializer.Serialize(new
                {
                    type = "object",
                    properties = new
                    {
                        paths = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "string"
                            },
                            description = "Array of file paths to read"
                        }
                    },
                    required = new[] { "paths" },
                    additionalProperties = false
                }),
                Tags = new[] { "File System" },
                Metadata = new Dictionary<string, object>
                {
                    { "friendly_name", "Read Multiple Files" },
                    { "priority", 11 }
                }
            };

            registry.RegisterTool(toolDefinition, ReadMultipleFilesHandler);
        }

        /// <summary>
        /// Handle multiple file read requests
        /// </summary>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        private async Task<object> ReadMultipleFilesHandler(object input)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var inputParams = JsonSerializer.Deserialize<ReadMultipleFilesParams>(
                    JsonSerializer.Serialize(input), options);

                if (inputParams?.Paths == null || inputParams.Paths.Length == 0)
                    return new Dictionary<string, object> { ["error"] = "Paths parameter is required and must not be empty" };

                var results = new List<object>();
                var tasks = new List<Task<(string path, string content, string error)>>();

                // Start all read operations asynchronously
                foreach (var path in inputParams.Paths)
                {
                    tasks.Add(ReadFileAsync(path));
                }

                // Wait for all tasks to complete
                var fileResults = await Task.WhenAll(tasks);

                // Process results
                foreach (var result in fileResults)
                {
                    if (result.error != null)
                    {
                        var errorResult = new Dictionary<string, object> { ["path"] = result.path, ["error"] = result.error };
                        results.Add(errorResult);
                    }
                    else
                    {
                        var contentResult = new Dictionary<string, object> { ["path"] = result.path, ["content"] = result.content };
                        results.Add(contentResult);
                    }
                }

                return new Dictionary<string, object> { ["files"] = results };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { ["error"] = $"Error processing file reading: {ex.Message}" };
            }
        }

        private async Task<(string path, string content, string error)> ReadFileAsync(string path)
        {
            if (!IsPathAllowed(path))
                return (path, null, $"Access to path '{path}' is not allowed");

            if (!File.Exists(path))
                return (path, null, $"File not found: {path}");

            try
            {
                var content = await File.ReadAllTextAsync(path);
                return (path, content, null);
            }
            catch (Exception ex)
            {
                return (path, null, ex.Message);
            }
        }

        /// <summary>
        /// Register a tool to write file contents
        /// </summary>
        private void RegisterWriteFileTool(IToolRegistry registry)
        {
            var toolDefinition = new ToolDefinition
            {
                Name = "write_file",
                Description = "Create a new file or completely overwrite an existing file with new content. Use with caution as it will overwrite existing files without warning. Handles text content with proper encoding. Only works within allowed directories.",
                Schema = JsonSerializer.Serialize(new
                {
                    type = "object",
                    properties = new
                    {
                        path = new
                        {
                            type = "string",
                            description = "The path to the file"
                        },
                        content = new
                        {
                            type = "string",
                            description = "The content to write to the file"
                        }
                    },
                    required = new[] { "path", "content" },
                    additionalProperties = false
                }),
                Tags = new[] { "File System" },
                Metadata = new Dictionary<string, object>
                {
                    { "friendly_name", "Write File" },
                    { "priority", 12 }
                }
            };

            registry.RegisterTool(toolDefinition, WriteFileHandler);
        }

        /// <summary>
        /// Handle file write requests
        /// </summary>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        private async Task<object> WriteFileHandler(object input)
        {
            var options = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(input));
            
            if (!options.TryGetValue("path", out var pathObj) || pathObj == null)
            {
                return new Dictionary<string, object> { ["error"] = "Path parameter is required" };
            }
            
            if (!options.TryGetValue("content", out var contentObj) || contentObj == null)
            {
                return new Dictionary<string, object> { ["error"] = "Content parameter is required" };
            }
            
            string path = pathObj.ToString();
            string content = contentObj.ToString();
            
            if (!IsPathAllowed(path))
            {
                return new Dictionary<string, object> { ["error"] = $"Access to path '{path}' is not allowed. Use list_allowed_directories to see available directories." };
            }
            
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    if (!IsPathAllowed(directory))
                    {
                        return new Dictionary<string, object> { ["error"] = $"Access to directory '{directory}' is not allowed" };
                    }
                        
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(path, content);
                return CreateSuccessResponse($"File written to {path}");
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { ["error"] = $"Error writing file: {ex.Message}" };
            }
        }

        /// <summary>
        /// Register a tool to make line-based edits to a file
        /// </summary>
        private void RegisterEditFileTool(IToolRegistry registry)
        {
            var toolDefinition = new ToolDefinition
            {
                Name = "edit_file",
                Description = "Make line-based edits to a text file. Each edit replaces exact line sequences with new content. Returns a git-style diff showing the changes made. Only works within allowed directories.",
                Schema = JsonSerializer.Serialize(new
                {
                    type = "object",
                    properties = new
                    {
                        path = new
                        {
                            type = "string",
                            description = "The path to the file"
                        },
                        edits = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    oldText = new
                                    {
                                        type = "string",
                                        description = "Text to search for - must match exactly"
                                    },
                                    newText = new
                                    {
                                        type = "string",
                                        description = "Text to replace with"
                                    }
                                },
                                required = new[] { "oldText", "newText" }
                            },
                            description = "Array of edit operations"
                        },
                        dryRun = new
                        {
                            type = "boolean",
                            description = "Preview changes using git-style diff format",
                            @default = false
                        }
                    },
                    required = new[] { "path", "edits" },
                    additionalProperties = false
                }),
                Tags = new[] { "File System" },
                Metadata = new Dictionary<string, object>
                {
                    { "friendly_name", "Edit File" },
                    { "priority", 13 }
                }
            };

            registry.RegisterTool(toolDefinition, EditFileHandler);
        }

        /// <summary>
        /// Handle file edit requests
        /// </summary>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        private async Task<object> EditFileHandler(object input)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var inputParams = JsonSerializer.Deserialize<EditFileParams>(
                    JsonSerializer.Serialize(input), options);

                if (inputParams == null)
                    return new Dictionary<string, object> { ["error"] = "Invalid input parameters" };

                if (string.IsNullOrEmpty(inputParams.Path))
                    return new Dictionary<string, object> { ["error"] = "Path parameter is required" };

                if (inputParams.Edits == null || inputParams.Edits.Length == 0)
                    return new Dictionary<string, object> { ["error"] = "Edits parameter is required and must not be empty" };

                if (!IsPathAllowed(inputParams.Path))
                    return new Dictionary<string, object> { ["error"] = $"Access to path '{inputParams.Path}' is not allowed. Use list_allowed_directories to see available directories." };

                if (!File.Exists(inputParams.Path))
                    return new Dictionary<string, object> { ["error"] = $"File not found: {inputParams.Path}" };

                // Read the file content
                string originalContent = await File.ReadAllTextAsync(inputParams.Path);
                string modifiedContent = originalContent;

                // Apply all edits
                foreach (var edit in inputParams.Edits)
                {
                    if (string.IsNullOrEmpty(edit.OldText))
                        return new Dictionary<string, object> { ["error"] = "oldText cannot be empty" };

                    modifiedContent = modifiedContent.Replace(edit.OldText, edit.NewText ?? "");
                }

                // Generate a diff
                string diff = GenerateDiff(inputParams.Path, originalContent, modifiedContent);

                // If not a dry run, write the changes
                if (!inputParams.DryRun)
                {
                    await File.WriteAllTextAsync(inputParams.Path, modifiedContent);
                    return new Dictionary<string, object>
                    {
                        ["success"] = true,
                        ["diff"] = diff
                    };
                }
                else
                {
                    return new Dictionary<string, object>
                    {
                        ["success"] = true,
                        ["diff"] = diff,
                        ["isDryRun"] = true
                    };
                }
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { ["error"] = $"Error editing file: {ex.Message}" };
            }
        }

        private string GenerateDiff(string path, string originalContent, string modifiedContent)
        {
            // Generate a simple git-style diff
            var originalLines = originalContent.Split('\n');
            var modifiedLines = modifiedContent.Split('\n');

            var diff = new StringBuilder();
            diff.AppendLine($"--- a/{path}");
            diff.AppendLine($"+++ b/{path}");

            // Very simple diff - this could be improved with a proper diff algorithm
            if (originalContent == modifiedContent)
            {
                diff.AppendLine("No changes");
            }
            else
            {
                // For simplicity, show all original lines as removed and all new lines as added
                // A proper implementation would use a diff algorithm to show only changes
                diff.AppendLine("@@ -1," + originalLines.Length + " +1," + modifiedLines.Length + " @@");
                
                // Show removed lines
                foreach (var line in originalLines)
                {
                    diff.AppendLine("- " + line);
                }
                
                // Show added lines
                foreach (var line in modifiedLines)
                {
                    diff.AppendLine("+ " + line);
                }
            }

            return diff.ToString();
        }

        /// <summary>
        /// Register a tool to create directories
        /// </summary>
        private void RegisterCreateDirectoryTool(IToolRegistry registry)
        {
            var toolDefinition = new ToolDefinition
            {
                Name = "create_directory",
                Description = "Create a new directory or ensure a directory exists. Can create multiple nested directories in one operation. If the directory already exists, this operation will succeed silently. Perfect for setting up directory structures for projects or ensuring required paths exist. Only works within allowed directories.",
                Schema = JsonSerializer.Serialize(new
                {
                    type = "object",
                    properties = new
                    {
                        path = new
                        {
                            type = "string",
                            description = "The path to the directory to create"
                        }
                    },
                    required = new[] { "path" },
                    additionalProperties = false
                }),
                Tags = new[] { "File System" },
                Metadata = new Dictionary<string, object>
                {
                    { "friendly_name", "Create Directory" },
                    { "priority", 14 }
                }
            };

            registry.RegisterTool(toolDefinition, CreateDirectoryHandler);
        }

        /// <summary>
        /// Handle directory creation requests
        /// </summary>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        private Task<object> CreateDirectoryHandler(object input)
        {
            var options = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(input));
            
            if (!options.TryGetValue("path", out var pathObj) || pathObj == null)
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = "Path parameter is required" });
            }
            
            string path = pathObj.ToString();
            
            if (!IsPathAllowed(path))
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Access to path '{path}' is not allowed. Use list_allowed_directories to see available directories." });
            }
            
            try
            {
                // Check if directory already exists
                if (Directory.Exists(path))
                {
                    return Task.FromResult<object>(CreateSuccessResponse($"Directory '{path}' already exists"));
                }

                // Create directory (and any necessary parent directories)
                Directory.CreateDirectory(path);
                return Task.FromResult<object>(CreateSuccessResponse($"Directory '{path}' created successfully"));
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Error creating directory: {ex.Message}" });
            }
        }

        /// <summary>
        /// Register a tool to list directory contents
        /// </summary>
        private void RegisterListDirectoryTool(IToolRegistry registry)
        {
            var toolDefinition = new ToolDefinition
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
                            type = "string",
                            description = "The path to the directory"
                        }
                    },
                    required = new[] { "path" },
                    additionalProperties = false
                }),
                Tags = new[] { "File System" },
                Metadata = new Dictionary<string, object>
                {
                    { "friendly_name", "List Directory" },
                    { "priority", 15 }
                }
            };

            registry.RegisterTool(toolDefinition, ListDirectoryHandler);
        }

        /// <summary>
        /// Handle directory listing requests
        /// </summary>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        private Task<object> ListDirectoryHandler(object input)
        {
            var options = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(input));
            
            if (!options.TryGetValue("path", out var pathObj) || pathObj == null)
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = "Path parameter is required" });
            }
            
            string path = pathObj.ToString();
            
            if (!IsPathAllowed(path))
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Access to path '{path}' is not allowed. Use list_allowed_directories to see available directories." });
            }
            
            if (!Directory.Exists(path))
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Directory not found: {path}" });
            }
            
            try
            {
                var entries = new List<object>();
                
                // Get directories
                foreach (var dir in Directory.GetDirectories(path))
                {
                    var dirInfo = new DirectoryInfo(dir);
                    var dirEntry = new Dictionary<string, object>
                    {
                        ["name"] = Path.GetFileName(dir),
                        ["type"] = "directory",
                        ["path"] = dir,
                        ["displayName"] = $"[DIR] {Path.GetFileName(dir)}"
                    };
                    entries.Add(dirEntry);
                }
                
                // Get files
                foreach (var file in Directory.GetFiles(path))
                {
                    var fileInfo = new FileInfo(file);
                    var fileEntry = new Dictionary<string, object>
                    {
                        ["name"] = fileInfo.Name,
                        ["type"] = "file",
                        ["path"] = file,
                        ["size"] = fileInfo.Length,
                        ["displayName"] = $"[FILE] {fileInfo.Name} ({FormatFileSize(fileInfo.Length)})"
                    };
                    entries.Add(fileEntry);
                }

                var directoryListingResult = new Dictionary<string, object>
                {
                    ["entriesList"] = entries,
                    ["dirPath"] = path,
                    ["totalEntries"] = entries.Count,
                    ["directoryCount"] = entries.Count(e => ((dynamic)e).type == "directory"),
                    ["fileCount"] = entries.Count(e => ((dynamic)e).type == "file")
                };
                
                return Task.FromResult<object>(directoryListingResult);
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Error listing directory: {ex.Message}" });
            }
        }

        /// <summary>
        /// Register a tool to get a directory tree
        /// </summary>
        private void RegisterDirectoryTreeTool(IToolRegistry registry)
        {
            var toolDefinition = new ToolDefinition
            {
                Name = "directory_tree",
                Description = "Get a recursive tree view of files and directories as a JSON structure. Each entry includes 'name', 'type' (file/directory), and 'children' for directories. Files have no children array, while directories always have a children array (which may be empty). The output is formatted with 2-space indentation for readability. Only works within allowed directories.",
                Schema = JsonSerializer.Serialize(new
                {
                    type = "object",
                    properties = new
                    {
                        path = new
                        {
                            type = "string",
                            description = "The path to the root directory"
                        }
                    },
                    required = new[] { "path" },
                    additionalProperties = false
                }),
                Tags = new[] { "File System" },
                Metadata = new Dictionary<string, object>
                {
                    { "friendly_name", "Directory Tree" },
                    { "priority", 16 }
                }
            };

            registry.RegisterTool(toolDefinition, DirectoryTreeHandler);
        }

        /// <summary>
        /// Handle directory tree requests
        /// </summary>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        private Task<object> DirectoryTreeHandler(object input)
        {
            var options = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(input));
            
            if (!options.TryGetValue("path", out var pathObj) || pathObj == null)
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = "Path parameter is required" });
            }
            
            string path = pathObj.ToString();
            
            if (!IsPathAllowed(path))
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Access to path '{path}' is not allowed. Use list_allowed_directories to see available directories." });
            }
            
            if (!Directory.Exists(path))
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Directory not found: {path}" });
            }
            
            try
            {
                var rootDir = new DirectoryInfo(path);
                var directories = new List<Dictionary<string, object>>();
                var files = new List<Dictionary<string, object>>();
                
                // Process the root directory first
                var rootContents = GetFullDirectoryContents(rootDir, directories, files);
                
                // Return the properly structured result
                return Task.FromResult<object>(new Dictionary<string, object>
                {
                    ["directories"] = directories,
                    ["files"] = files,
                    ["path"] = path,
                    ["count"] = directories.Count + files.Count
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Error building directory tree: {ex.Message}" });
            }
        }

        private void GetFullDirectoryContents(DirectoryInfo directory, List<Dictionary<string, object>> directories, List<Dictionary<string, object>> files)
        {
            try
            {
                // Process directories
                foreach (var subDir in directory.GetDirectories())
                {
                    var dirDict = new Dictionary<string, object>
                    {
                        ["name"] = subDir.Name,
                        ["path"] = subDir.FullName,
                        ["exists"] = true,
                        ["contents"] = new Dictionary<string, object>
                        {
                            ["files"] = new List<Dictionary<string, object>>(),
                            ["directories"] = new List<Dictionary<string, object>>()
                        }
                    };
                    
                    directories.Add(dirDict);
                    
                    // Recursively process subdirectories
                    var subDirContents = dirDict["contents"] as Dictionary<string, object>;
                    var subDirs = subDirContents["directories"] as List<Dictionary<string, object>>;
                    var subFiles = subDirContents["files"] as List<Dictionary<string, object>>;
                    
                    // Add subdirectory contents
                    try
                    {
                        foreach (var nestedDir in subDir.GetDirectories())
                        {
                            var nestedDirDict = new Dictionary<string, object>
                            {
                                ["name"] = nestedDir.Name,
                                ["path"] = nestedDir.FullName,
                                ["exists"] = true
                            };
                            subDirs.Add(nestedDirDict);
                        }
                        
                        // Add files in the subdirectory
                        foreach (var file in subDir.GetFiles())
                        {
                            var fileDict = new Dictionary<string, object>
                            {
                                ["name"] = file.Name,
                                ["path"] = file.FullName,
                                ["size"] = file.Length,
                                ["exists"] = true
                            };
                            subFiles.Add(fileDict);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip directories we can't access
                    }
                }
                
                // Process files
                foreach (var file in directory.GetFiles())
                {
                    var fileDict = new Dictionary<string, object>
                    {
                        ["name"] = file.Name,
                        ["path"] = file.FullName,
                        ["size"] = file.Length,
                        ["exists"] = true
                    };
                    files.Add(fileDict);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
        }

        /// <summary>
        /// Register a tool to move or rename files
        /// </summary>
        private void RegisterMoveFileTool(IToolRegistry registry)
        {
            var toolDefinition = new ToolDefinition
            {
                Name = "move_file",
                Description = "Move or rename files and directories. Can move files between directories and rename them in a single operation. If the destination exists, the operation will fail. Works across different directories and can be used for simple renaming within the same directory. Both source and destination must be within allowed directories.",
                Schema = JsonSerializer.Serialize(new
                {
                    type = "object",
                    properties = new
                    {
                        source = new
                        {
                            type = "string",
                            description = "The source file or directory path"
                        },
                        destination = new
                        {
                            type = "string",
                            description = "The destination file or directory path"
                        }
                    },
                    required = new[] { "source", "destination" },
                    additionalProperties = false
                }),
                Tags = new[] { "File System" },
                Metadata = new Dictionary<string, object>
                {
                    { "friendly_name", "Move File" },
                    { "priority", 17 }
                }
            };

            registry.RegisterTool(toolDefinition, MoveFileHandler);
        }

        /// <summary>
        /// Handle file move requests
        /// </summary>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        private Task<object> MoveFileHandler(object input)
        {
            var options = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(input));
            
            if (!options.TryGetValue("source", out var sourceObj) || sourceObj == null)
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = "Source parameter is required" });
            }
            
            if (!options.TryGetValue("destination", out var destinationObj) || destinationObj == null)
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = "Destination parameter is required" });
            }
            
            string source = sourceObj.ToString();
            string destination = destinationObj.ToString();
            
            if (!IsPathAllowed(source))
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Access to source path '{source}' is not allowed. Use list_allowed_directories to see available directories." });
            }
            
            if (!IsPathAllowed(destination))
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Access to destination path '{destination}' is not allowed. Use list_allowed_directories to see available directories." });
            }
            
            // Check if source exists
            bool isDirectory = Directory.Exists(source);
            bool isFile = File.Exists(source);
            
            if (!isDirectory && !isFile)
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Source not found: {source}" });
            }
            
            // Check if destination already exists
            if (File.Exists(destination) || Directory.Exists(destination))
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Destination already exists: {destination}" });
            }
            
            try
            {
                // Create destination directory if needed
                string destinationDir = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                // Move the file or directory
                if (isDirectory)
                {
                    Directory.Move(source, destination);
                    return Task.FromResult<object>(CreateSuccessResponse(
                        $"Directory moved from '{source}' to '{destination}'", 
                        "directory"));
                }
                else
                {
                    File.Move(source, destination);
                    return Task.FromResult<object>(CreateSuccessResponse(
                        $"File moved from '{source}' to '{destination}'", 
                        "file"));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Error moving file: {ex.Message}" });
            }
        }

        /// <summary>
        /// Register a tool to search for files
        /// </summary>
        private void RegisterSearchFilesTool(IToolRegistry registry)
        {
            var toolDefinition = new ToolDefinition
            {
                Name = "search_files",
                Description = "Recursively search for files and directories matching a pattern. Searches through all subdirectories from the starting path. The search is case-insensitive and matches partial names. Returns full paths to all matching items. Great for finding files when you don't know their exact location. Only searches within allowed directories.",
                Schema = JsonSerializer.Serialize(new
                {
                    type = "object",
                    properties = new
                    {
                        path = new
                        {
                            type = "string",
                            description = "The directory to search in"
                        },
                        pattern = new
                        {
                            type = "string",
                            description = "The search pattern (e.g., *.txt)"
                        },
                        excludePatterns = new
                        {
                            type = "array",
                            items = new
                            {
                                type = "string"
                            },
                            description = "Patterns to exclude from results",
                            @default = new string[0]
                        }
                    },
                    required = new[] { "path", "pattern" },
                    additionalProperties = false
                }),
                Tags = new[] { "File System" },
                Metadata = new Dictionary<string, object>
                {
                    { "friendly_name", "Search Files" },
                    { "priority", 18 }
                }
            };

            registry.RegisterTool(toolDefinition, SearchFilesHandler);
        }

        /// <summary>
        /// Handle file search requests
        /// </summary>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        private Task<object> SearchFilesHandler(object input)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var inputParams = JsonSerializer.Deserialize<SearchFilesParams>(
                    JsonSerializer.Serialize(input), options);

                if (inputParams == null)
                {
                    return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = "Invalid input parameters" });
                }

                if (string.IsNullOrEmpty(inputParams.Path))
                {
                    return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = "Path parameter is required" });
                }

                if (string.IsNullOrEmpty(inputParams.Pattern))
                {
                    return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = "Pattern parameter is required" });
                }

                if (!IsPathAllowed(inputParams.Path))
                {
                    return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Access to path '{inputParams.Path}' is not allowed. Use list_allowed_directories to see available directories." });
                }

                if (!Directory.Exists(inputParams.Path))
                {
                    return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Directory not found: {inputParams.Path}" });
                }

                var excludePatterns = inputParams.ExcludePatterns ?? Array.Empty<string>();
                var results = new List<object>();
                
                // Search for files
                var fileResults = Directory.GetFiles(inputParams.Path, inputParams.Pattern, SearchOption.AllDirectories)
                    .Where(file => !ShouldExclude(file, excludePatterns))
                    .Select(file => {
                        return new Dictionary<string, object>
                        {
                            ["name"] = Path.GetFileName(file),
                            ["path"] = file,
                            ["type"] = "file"
                        };
                    }).ToList<object>();
                
                results.AddRange(fileResults);
                
                // Search for directories that match the pattern
                // This requires a different approach since Directory.GetDirectories doesn't support wildcards the same way
                if (inputParams.Pattern.Contains("*") || inputParams.Pattern.Contains("?"))
                {
                    // Convert the pattern to a regex
                    var regexPattern = WildcardToRegex(inputParams.Pattern);
                    var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
                    
                    var dirResults = Directory.GetDirectories(inputParams.Path, "*", SearchOption.AllDirectories)
                        .Where(dir => !ShouldExclude(dir, excludePatterns) && regex.IsMatch(Path.GetFileName(dir)))
                        .Select(dir => {
                            return new Dictionary<string, object>
                            {
                                ["name"] = Path.GetFileName(dir),
                                ["path"] = dir,
                                ["type"] = "directory"
                            };
                        }).ToList<object>();
                    
                    results.AddRange(dirResults);
                }

                var searchResultDict = new Dictionary<string, object>
                {
                    ["foundItems"] = results,
                    ["itemCount"] = results.Count,
                    ["basePath"] = inputParams.Path,
                    ["searchQuery"] = inputParams.Pattern
                };
                
                return Task.FromResult<object>(searchResultDict);
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Error searching files: {ex.Message}" });
            }
        }

        private bool ShouldExclude(string path, string[] excludePatterns)
        {
            if (excludePatterns == null || excludePatterns.Length == 0)
                return false;
                
            var filename = Path.GetFileName(path);
            
            foreach (var pattern in excludePatterns)
            {
                var regexPattern = WildcardToRegex(pattern);
                var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
                
                if (regex.IsMatch(filename) || regex.IsMatch(path))
                    return true;
            }
            
            return false;
        }

        private string WildcardToRegex(string pattern)
        {
            return "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
        }

        /// <summary>
        /// Register a tool to get file info
        /// </summary>
        private void RegisterGetFileInfoTool(IToolRegistry registry)
        {
            var toolDefinition = new ToolDefinition
            {
                Name = "get_file_info",
                Description = "Retrieve detailed metadata about a file or directory. Returns comprehensive information including size, creation time, last modified time, permissions, and type. This tool is perfect for understanding file characteristics without reading the actual content. Only works within allowed directories.",
                Schema = JsonSerializer.Serialize(new
                {
                    type = "object",
                    properties = new
                    {
                        path = new
                        {
                            type = "string",
                            description = "The path to the file or directory"
                        }
                    },
                    required = new[] { "path" },
                    additionalProperties = false
                }),
                Tags = new[] { "File System" },
                Metadata = new Dictionary<string, object>
                {
                    { "friendly_name", "Get File Info" },
                    { "priority", 19 }
                }
            };

            registry.RegisterTool(toolDefinition, GetFileInfoHandler);
        }

        /// <summary>
        /// Handle file info requests
        /// </summary>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        private Task<object> GetFileInfoHandler(object input)
        {
            var options = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(input));
            
            if (!options.TryGetValue("path", out var pathObj) || pathObj == null)
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = "Path parameter is required" });
            }
            
            string path = pathObj.ToString();
            
            if (!IsPathAllowed(path))
            {
                return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Access to path '{path}' is not allowed. Use list_allowed_directories to see available directories." });
            }
            
            try
            {
                bool isDirectory = Directory.Exists(path);
                bool isFile = File.Exists(path);
                
                if (!isDirectory && !isFile)
                {
                    return Task.FromResult<object>(new Dictionary<string, object> { ["error"] = $"Path not found: {path}" });
                }
                    
                if (isDirectory)
                {
                    var dirInfo = new DirectoryInfo(path);
                    var dirInfoResult = new Dictionary<string, object>
                    {
                        ["name"] = dirInfo.Name,
                        ["fullPath"] = dirInfo.FullName,
                        ["type"] = "directory",
                        ["created"] = dirInfo.CreationTime,
                        ["lastModified"] = dirInfo.LastWriteTime,
                        ["lastAccessed"] = dirInfo.LastAccessTime,
                        ["attributes"] = dirInfo.Attributes.ToString(),
                        ["parent"] = dirInfo.Parent?.FullName
                    };
                    return Task.FromResult<object>(dirInfoResult);
                }
                else
                {
                    var fileInfo = new FileInfo(path);
                    var fileInfoResult = new Dictionary<string, object>
                    {
                        ["name"] = fileInfo.Name,
                        ["fullPath"] = fileInfo.FullName,
                        ["type"] = "file",
                        ["size"] = fileInfo.Length,
                        ["sizeFormatted"] = FormatFileSize(fileInfo.Length),
                        ["extension"] = fileInfo.Extension,
                        ["created"] = fileInfo.CreationTime,
                        ["lastModified"] = fileInfo.LastWriteTime,
                        ["lastAccessed"] = fileInfo.LastAccessTime,
                        ["attributes"] = fileInfo.Attributes.ToString(),
                        ["directory"] = fileInfo.DirectoryName,
                        ["isReadOnly"] = fileInfo.IsReadOnly
                    };
                    return Task.FromResult<object>(fileInfoResult);
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(new Dictionary<string, object>
                {
                    ["error"] = $"Error getting file info: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Register a tool to list allowed directories
        /// </summary>
        private void RegisterListAllowedDirectoriesTool(IToolRegistry registry)
        {
            var toolDefinition = new ToolDefinition
            {
                Name = "list_allowed_directories",
                Description = "Returns the list of directories that this server is allowed to access. Use this to understand which directories are available before trying to access files.",
                Schema = JsonSerializer.Serialize(new
                {
                    type = "object",
                    properties = new { },
                    additionalProperties = false
                }),
                Tags = new[] { "File System" },
                Metadata = new Dictionary<string, object>
                {
                    { "friendly_name", "List Allowed Directories" },
                    { "priority", 20 }
                }
            };

            registry.RegisterTool(toolDefinition, ListAllowedDirectoriesHandler);
        }

        /// <summary>
        /// Handle list allowed directories requests
        /// </summary>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        private async Task<object> ListAllowedDirectoriesHandler(object input)
        {
            try
            {
                var dirInfoCollection = _allowedDirectories.Select(dir => new Dictionary<string, object>
                {
                    ["path"] = dir,
                    ["exists"] = Directory.Exists(dir)
                }).ToList();
                
                var resultDict = new Dictionary<string, object>
                {
                    ["directories"] = dirInfoCollection,
                    ["count"] = _allowedDirectories.Count
                };
                
                return resultDict;
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    ["error"] = $"Error listing allowed directories: {ex.Message}"
                };
            }
        }
        
        #endregion

        #region Helper Classes & Methods
        
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
        /// Parameters for reading multiple files
        /// </summary>
        private class ReadMultipleFilesParams
        {
            public string[] Paths { get; set; }
        }

        /// <summary>
        /// Parameters for editing a file
        /// </summary>
        private class EditFileParams
        {
            public string Path { get; set; }
            public EditOperation[] Edits { get; set; }
            public bool DryRun { get; set; }
        }

        /// <summary>
        /// Represents a single edit operation
        /// </summary>
        private class EditOperation
        {
            public string OldText { get; set; }
            public string NewText { get; set; }
        }

        /// <summary>
        /// Parameters for searching files
        /// </summary>
        private class SearchFilesParams
        {
            public string Path { get; set; }
            public string Pattern { get; set; }
            public string[] ExcludePatterns { get; set; }
        }
        
        #endregion
    }
}