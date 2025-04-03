using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.API_Clients.MCP
{
    /// <summary>
    /// Client for communicating with an external filesystem MCP server
    /// </summary>
    public class FileSystemMCPServerClient : IMCPServerClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _serverUrl;
        private List<ToolDefinition> _cachedTools;
        
        /// <summary>
        /// Creates a new FileSystemMCPServerClient
        /// </summary>
        /// <param name="serverUrl">URL of the filesystem MCP server</param>
        public FileSystemMCPServerClient(string serverUrl)
        {
            _serverUrl = serverUrl ?? throw new ArgumentNullException(nameof(serverUrl));
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }
        
        /// <summary>
        /// Gets the available tools from the server
        /// </summary>
        public async Task<List<ToolDefinition>> GetAvailableToolsAsync()
        {
            // Return cached tools if available
            if (_cachedTools != null)
            {
                return _cachedTools;
            }
            
            try
            {
                var response = await _httpClient.GetAsync($"{_serverUrl}/tools");
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var tools = JsonSerializer.Deserialize<List<ToolDefinition>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                // Add server metadata to each tool
                foreach (var tool in tools)
                {
                    if (tool.Metadata == null)
                    {
                        tool.Metadata = new Dictionary<string, object>();
                    }
                    
                    tool.Metadata["server_name"] = "filesystem";
                }
                
                // Cache tools for future use
                _cachedTools = tools;
                
                return tools;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting available tools: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Executes a tool on the server
        /// </summary>
        public async Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters)
        {
            try
            {
                var request = new
                {
                    tool = toolName,
                    parameters = parameters
                };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json");
                    
                var response = await _httpClient.PostAsync($"{_serverUrl}/execute", content);
                response.EnsureSuccessStatusCode();
                
                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<object>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing tool {toolName}: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Checks if the server is available
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_serverUrl}/ping");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
