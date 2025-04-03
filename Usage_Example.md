# Enhanced MCP Usage Examples

This document provides examples of how to use the enhanced MCP implementation in your application.

## Basic Usage

The enhanced MCP implementation is designed to be a drop-in replacement for the existing MCP implementation. If you're using the `MCPServiceRegistration` class to register MCP services, you don't need to make any changes to your code.

```csharp
// In your application startup code
MCPServiceRegistration.RegisterMCPServices();
```

This will automatically use the enhanced Ollama MCP adapter when appropriate.

## Manually Creating the Enhanced Adapter

If you need to create the enhanced adapter manually, you can do so like this:

```csharp
// Get the necessary services
var ollamaClient = (OllamaClient)LLMClientFactory.GetClient(LLMClientFactory.ProviderType.Ollama);
var messageParsingService = ServiceProvider.GetService<IMessageParsingService>();
var toolRegistry = ServiceProvider.GetService<IToolRegistry>();

// Create the enhanced adapter
var enhancedAdapter = new EnhancedOllamaMCPAdapter(ollamaClient, messageParsingService, toolRegistry);

// Register it with the service provider
ServiceProvider.RegisterService<IMCPLLMClient>(enhancedAdapter);
```

## Connecting to an External FileSystem MCP Server

Here's how to connect to an external FileSystem MCP server:

```csharp
// Create the server client
var fileSystemServer = new FileSystemMCPServerClient("http://localhost:3000");

// Check if the server is available
if (await fileSystemServer.IsAvailableAsync())
{
    // Get the MCP client factory
    var mcpClientFactory = ServiceProvider.GetService<MCPClientFactory>();

    // Register the server
    mcpClientFactory.RegisterMCPServer("filesystem", fileSystemServer);

    // Get the tools from the server
    var tools = await fileSystemServer.GetAvailableToolsAsync();

    // Get the tool registry
    var toolRegistry = ServiceProvider.GetService<IToolRegistry>();

    // Register the tools with the registry
    foreach (var tool in tools)
    {
        toolRegistry.RegisterTool(tool, async (parameters) => {
            // Execute the tool on the server
            return await fileSystemServer.ExecuteToolAsync(tool.Name, parameters);
        });
    }
}
```

## Using MCP in ChatSessionViewModel

The `ChatSessionViewModel` already has comprehensive MCP support, so you don't need to make any changes to your code. It will automatically use the enhanced adapter when appropriate.

```csharp
// In your view code
var chatViewModel = new ChatSessionViewModel(
    ServiceProvider.GetService<IMCPLLMClientService>(),
    ServiceProvider.GetService<IChatSessionService>(),
    ServiceProvider.GetService<IMCPContextManager>(),
    ServiceProvider.GetService<IMessageParsingService>());

// Send a message that might use tools
await chatViewModel.SubmitInput();
```

## Advanced: Creating a Custom MCP Server Client

If you need to create a custom MCP server client, you can implement the `IMCPServerClient` interface:

```csharp
public class CustomMCPServerClient : IMCPServerClient
{
    private readonly HttpClient _httpClient;
    private readonly string _serverUrl;
    
    public CustomMCPServerClient(string serverUrl)
    {
        _serverUrl = serverUrl;
        _httpClient = new HttpClient();
    }
    
    public async Task<List<ToolDefinition>> GetAvailableToolsAsync()
    {
        // Implement your logic to get available tools
        // ...
    }
    
    public async Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters)
    {
        // Implement your logic to execute a tool
        // ...
    }
    
    public async Task<bool> IsAvailableAsync()
    {
        // Implement your logic to check if the server is available
        // ...
    }
}
```

Then register it with the MCP client factory:

```csharp
var customServer = new CustomMCPServerClient("http://example.com/mcp");
var mcpClientFactory = ServiceProvider.GetService<MCPClientFactory>();
mcpClientFactory.RegisterMCPServer("custom", customServer);
```

## Common Troubleshooting

### Tool Not Found

If you're getting a "Tool not found" error, make sure the tool is registered with the tool registry:

```csharp
// Check if the tool exists
var toolRegistry = ServiceProvider.GetService<IToolRegistry>();
if (!toolRegistry.ToolExists("your_tool_name"))
{
    // Register the tool
    // ...
}
```

### Server Not Available

If an external MCP server is not available, the adapter will fall back to using local tools. You can check if a server is available:

```csharp
var mcpClientFactory = ServiceProvider.GetService<MCPClientFactory>();
var server = mcpClientFactory.GetMCPServer("server_name");
if (server != null && await server.IsAvailableAsync())
{
    // The server is available
    // ...
}
else
{
    // The server is not available
    // ...
}
```

### Model Not Using Tools

If a model is not using tools when it should, you can check if it supports MCP:

```csharp
var llmClientService = ServiceProvider.GetService<IMCPLLMClientService>();
if (llmClientService.ModelSupportsMCP("your_model_name"))
{
    // The model supports MCP
    // ...
}
else
{
    // The model does not support MCP
    // ...
}
```

You can also try using a different model that has better tool-calling capabilities, such as:

- llama3:instruct
- mixtral
- qwen
- gemma:instruct
