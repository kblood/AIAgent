using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using System.Net.Http;
using System.Linq;

namespace AIAgentTest.Services.MCP
{
    /// <summary>
    /// Common tools for MCP
    /// </summary>
    public class CommonTools
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        
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
            
            //// Register file read tool
            //registry.RegisterTool(
            //    new ToolDefinition
            //    {
            //        Name = "read_file",
            //        Description = "Read the contents of a file",
            //        Schema = JsonSerializer.Serialize(new
            //        {
            //            type = "object",
            //            properties = new
            //            {
            //                path = new
            //                {
            //                    type = "string",
            //                    description = "The path to the file"
            //                }
            //            },
            //            required = new[] { "path" }
            //        }),
            //        Tags = new[] { "File System" }
            //    },
            //    ReadFileHandler);
            
            //// Register directory listing tool
            //registry.RegisterTool(
            //    new ToolDefinition
            //    {
            //        Name = "list_directory",
            //        Description = "List files and directories in a directory",
            //        Schema = JsonSerializer.Serialize(new
            //        {
            //            type = "object",
            //            properties = new
            //            {
            //                path = new
            //                {
            //                    type = "string",
            //                    description = "The path to the directory"
            //                }
            //            },
            //            required = new[] { "path" }
            //        }),
            //        Tags = new[] { "File System" }
            //    },
            //    ListDirectoryHandler);
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
                return new { error = "Tool registry not available" };
            }
            
            // Get all registered tools
            var tools = toolRegistry.GetAllTools();
            
            // Format the tools for display
            var result = new
            {
                tools = tools.Select(t => new
                {
                    name = t.Name,
                    description = t.Description,
                    tags = t.Tags,
                    schema = t.Schema
                }).ToList()
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
            
            var result = new
            {
                localDateTime = now.ToString("yyyy-MM-dd HH:mm:ss"),
                utcDateTime = utcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                timeZone = TimeZoneInfo.Local.DisplayName,
                unixTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds()
            };
            
            Console.WriteLine($"GetDateTimeHandler returning: {JsonSerializer.Serialize(result)}");
            return result;
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
                return new { error = "Path parameter is required" };
            }
            
            string path = pathObj.ToString();
            
            try
            {
                if (!File.Exists(path))
                {
                    return new { error = "File not found" };
                }
                
                var content = await File.ReadAllTextAsync(path);
                return new { content };
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        }
        
        /// <summary>
        /// Handle directory listing requests
        /// </summary>
        /// <param name="input">Tool input</param>
        /// <returns>Tool result</returns>
        private async Task<object> ListDirectoryHandler(object input)
        {
            var options = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(input));
            
            if (!options.TryGetValue("path", out var pathObj) || pathObj == null)
            {
                return new { error = "Path parameter is required" };
            }
            
            string path = pathObj.ToString();
            
            try
            {
                if (!Directory.Exists(path))
                {
                    return new { error = "Directory not found" };
                }
                
                var directories = Directory.GetDirectories(path).Select(Path.GetFileName).ToArray();
                var files = Directory.GetFiles(path).Select(Path.GetFileName).ToArray();
                
                return new
                {
                    directories,
                    files
                };
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        }
    }
}
