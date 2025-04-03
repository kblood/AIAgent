# MCP Enhancement for AIAgent

This implementation adds the enhanced Ollama MCP adapter based on the ollama-mcp-bridge project. It provides better MCP capabilities for Ollama models by using more structured prompts and providing support for external MCP servers.

## Implemented Enhancements

1. **EnhancedOllamaMCPAdapter**: A new implementation of the IMCPLLMClient interface that provides improved MCP capabilities for Ollama models. It features:
   - Better prompt formatting for more reliable tool usage
   - Support for external MCP servers
   - Improved error handling and parsing
   - Lower temperature settings for more deterministic responses
   - Consistent format for tool calls and results

2. **IMCPServerClient Interface**: An interface for connecting to external MCP servers that can provide additional tools.

## Integration

To use the enhanced MCP adapter, you should register it with the MCPClientFactory like this:

```csharp
// In MCPClientFactory.cs
public IMCPLLMClient CreateMCPClient(string provider)
{
    switch (provider.ToLower())
    {
        case "ollama":
            var ollamaClient = (OllamaClient)LLMClientFactory.GetClient(LLMClientFactory.ProviderType.Ollama);
            if (ollamaClient == null)
            {
                throw new InvalidOperationException("Failed to create Ollama client");
            }
            
            // Use the enhanced adapter instead of the basic one
            return new EnhancedOllamaMCPAdapter(ollamaClient, _parsingService, _toolRegistry);
            
        // Add other providers as needed
            
        default:
            throw new NotSupportedException($"Provider {provider} is not supported for MCP");
    }
}
```

This change will allow all Ollama models to use the enhanced MCP capabilities.

## Adding External MCP Servers

You can add external MCP servers to the EnhancedOllamaMCPAdapter:

```csharp
// Create the server client
var fileSystemServer = new FileSystemMCPServerClient("http://localhost:3000");

// Register with the adapter
var mcpAdapter = (EnhancedOllamaMCPAdapter)mcpClientFactory.CreateMCPClient("ollama");
mcpAdapter.RegisterMCPServer("filesystem", fileSystemServer);
```

You would then need to implement the FileSystemMCPServerClient class that implements the IMCPServerClient interface.

## Test Suite

To verify that the enhanced adapter works correctly, you can run the following tests:

1. Generate a response with a simple calculator tool
2. Chain multiple tool calls together
3. Handle tool execution errors gracefully

## Implementation Status

The EnhancedOllamaMCPAdapter has been fully implemented and is ready for use. It is compatible with the existing MCP infrastructure and should work with all models supported by Ollama.

No changes are needed to the ChatSessionViewModel as it already has comprehensive MCP support, including handling of tool calls, displaying tool results, and continuing conversations with tool results.

## Performance Considerations

The enhanced adapter includes several optimizations that can improve the reliability of MCP with Ollama models:

1. **Lower Temperature**: The adapter uses a lower temperature setting (0.2) for generating tool calls, which helps ensure that the model follows the required format.

2. **Structured Prompts**: The tool descriptions are formatted in a clear and consistent way that makes it easier for the model to understand their purpose and parameters.

3. **Improved Error Handling**: The adapter includes more robust error handling, which helps prevent failed tool calls from disrupting the conversation.

## Adding New Tools

The existing MCP infrastructure makes it easy to add new tools. You can register new tools with the ToolRegistry:

```csharp
// Define the tool's input schema
var inputSchema = new Dictionary<string, object>
{
    ["type"] = "object",
    ["properties"] = new Dictionary<string, object>
    {
        ["query"] = new Dictionary<string, object>
        {
            ["type"] = "string",
            ["description"] = "Search query"
        }
    },
    ["required"] = new List<string> { "query" }
};

// Define the tool's output schema
var outputSchema = new Dictionary<string, object>
{
    ["type"] = "object",
    ["properties"] = new Dictionary<string, object>
    {
        ["results"] = new Dictionary<string, object>
        {
            ["type"] = "array",
            ["items"] = new Dictionary<string, object>
            {
                ["type"] = "object"
            }
        }
    }
};

// Create the tool definition
var toolDefinition = new ToolDefinition
{
    Name = "my_custom_tool",
    Description = "Performs a custom operation",
    Input = inputSchema,
    Output = outputSchema,
    ToolType = "function",
    Tags = new List<string> { "Custom" }
};

// Register the tool with a handler
toolRegistry.RegisterTool(toolDefinition, async (parameters) => {
    var query = parameters["query"].ToString();
    // Implement your tool logic here
    return new { results = new[] { new { title = $"Result for {query}" } } };
});
```

## External MCP Server Implementation

For implementing an external MCP server client, you can use the following pattern:

```csharp
public class FileSystemMCPServerClient : IMCPServerClient
{
    private readonly HttpClient _httpClient;
    private readonly string _serverUrl;
    
    public FileSystemMCPServerClient(string serverUrl)
    {
        _serverUrl = serverUrl;
        _httpClient = new HttpClient();
    }
    
    public async Task<List<ToolDefinition>> GetAvailableToolsAsync()
    {
        var response = await _httpClient.GetAsync($"{_serverUrl}/tools");
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<ToolDefinition>>(content);
    }
    
    public async Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters)
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
        return JsonSerializer.Deserialize<object>(responseContent);
    }
    
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
```

## Future Improvements

While the current implementation is quite robust, there are several areas for future enhancement:

1. **Tool Discovery**: Implement automatic discovery of tools from external MCP servers.

2. **Tool Permissions**: Add a permission system for sensitive tools that require user authorization.

3. **Tool Analytics**: Track tool usage statistics to identify which tools are most useful.

4. **Streaming Tool Results**: Support streaming results from long-running tool executions.

5. **Tool Chaining**: Implement more sophisticated tool chaining for complex workflows.

## Conclusion

The EnhancedOllamaMCPAdapter provides a significant improvement in MCP capabilities for Ollama models. It makes tool usage more reliable and adds support for external MCP servers, which can greatly extend the capabilities of the AIAgent application.
