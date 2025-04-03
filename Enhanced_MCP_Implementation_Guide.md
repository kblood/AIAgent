# Enhanced MCP Implementation Guide

This document provides instructions for implementing the enhanced MCP (Model Context Protocol) functionality in the AIAgent project.

## Files Created

1. **EnhancedOllamaMCPAdapter.cs**
   - Path: `AIAgentTest\API_Clients\MCP\EnhancedOllamaMCPAdapter.cs`
   - Purpose: Provides improved tool calling capabilities for Ollama models with structured prompts and support for external MCP servers.

2. **FileSystemMCPServerClient.cs**
   - Path: `AIAgentTest\API_Clients\MCP\FileSystemMCPServerClient.cs`
   - Purpose: Implements a client for connecting to an external filesystem MCP server.

3. **MCPServerRegistration.cs**
   - Path: `AIAgentTest\Services\MCP\MCPServerRegistration.cs`
   - Purpose: Handles registration of external MCP servers and their tools.

## Updates Required

The following files need to be updated:

1. **MCPClientFactory.cs**
   - Add support for the enhanced adapter and server registration.
   - See `MCPClientFactory_update.cs` for the full updated file.
   - Main changes:
     - Add `_toolRegistry` and `_serverClients` fields
     - Update constructor to accept tool registry
     - Enhance `CreateMCPClient` to use EnhancedOllamaMCPAdapter when available
     - Add methods for server registration and retrieval

2. **MCPServiceRegistration.cs**
   - Update to register MCP servers and add a convenience method for creating the enhanced adapter.
   - See `MCPServiceRegistration_update.cs` for the full updated file.
   - Main changes:
     - Pass tool registry to MCPClientFactory constructor
     - Add call to `MCPServerRegistration.RegisterMCPServersAsync`
     - Add `CreateEnhancedOllamaAdapter` method

## Implementation Steps

Follow these steps to implement the enhanced MCP functionality:

1. **Add the new files**:
   - Add `EnhancedOllamaMCPAdapter.cs` to the `API_Clients\MCP` folder
   - Add `FileSystemMCPServerClient.cs` to the `API_Clients\MCP` folder
   - Add `MCPServerRegistration.cs` to the `Services\MCP` folder

2. **Update existing files**:
   - Update `MCPClientFactory.cs` using the content from `MCPClientFactory_update.cs`
   - Update `MCPServiceRegistration.cs` using the content from `MCPServiceRegistration_update.cs`

## Key Improvements

1. **Better Tool Calling with Ollama**
   - More reliable tool calling format adherence with lower temperature settings
   - Structured prompts for better results
   - Improved error handling and response parsing

2. **External MCP Server Support**
   - Connect to external servers for expanded capabilities
   - Automatic tool discovery and registration
   - Server availability checking

3. **Enhanced Model Support**
   - Added support for newer models like gemma and qwen

## Using the Enhanced Functionality

### Creating an Enhanced Ollama MCP Adapter

```csharp
// Using the convenience method
var enhancedAdapter = MCPServiceRegistration.CreateEnhancedOllamaAdapter();

// Or get it from the MCPClientFactory
var mcpClientFactory = ServiceProvider.GetService<MCPClientFactory>();
var enhancedAdapter = (EnhancedOllamaMCPAdapter)mcpClientFactory.CreateMCPClient("ollama");
```

### Connecting to an External MCP Server

```csharp
var mcpClientFactory = ServiceProvider.GetService<MCPClientFactory>();
var fileSystemServer = new FileSystemMCPServerClient("http://localhost:3000");

// Register with the factory
mcpClientFactory.RegisterMCPServer("filesystem", fileSystemServer);

// Check if the server is available
if (await fileSystemServer.IsAvailableAsync())
{
    // Get tools from the server
    var tools = await fileSystemServer.GetAvailableToolsAsync();
    
    // Register tools with the registry
    var toolRegistry = ServiceProvider.GetService<IToolRegistry>();
    foreach (var tool in tools)
    {
        toolRegistry.RegisterTool(tool, async (parameters) => {
            return await fileSystemServer.ExecuteToolAsync(tool.Name, parameters);
        });
    }
}
```

### Using the Enhanced MCP in ChatSessionViewModel

The existing `ChatSessionViewModel` already has comprehensive MCP support, so no changes are needed. It will automatically use the enhanced capabilities if available.

## Debugging

If you encounter issues, check the following:

1. Make sure all namespaces are correctly referenced
2. Ensure the `IToolRegistry` is properly registered and available
3. Verify that the EnhancedOllamaMCPAdapter is being created correctly

## Example Usage

```csharp
// Get the MCP LLM client service
var mcpLLMClientService = ServiceProvider.GetService<IMCPLLMClientService>();

// Check if a model supports MCP
bool supportsMCP = mcpLLMClientService.ModelSupportsMCP("llama3:instruct");

// Get all available tools
var toolRegistry = ServiceProvider.GetService<IToolRegistry>();
var tools = toolRegistry.GetTools();

// Generate response with tools
var response = await mcpLLMClientService.GenerateWithMCPAsync(
    "What is the square root of 144?", 
    "llama3:instruct", 
    tools);

// Handle the response based on type
if (response.Type == "tool_use")
{
    // A tool was used
    Console.WriteLine($"Tool: {response.Tool}");
    Console.WriteLine($"Parameters: {JsonSerializer.Serialize(response.Input)}");
    
    // Execute the tool
    var toolResult = await mcpLLMClientService.ExecuteToolAsync(response.Tool, response.Input);
    
    // Continue the conversation with the tool result
    var continuedResponse = await mcpLLMClientService.ContinueWithToolResultAsync(
        "What is the square root of 144?", 
        response.Tool, 
        toolResult, 
        "llama3:instruct");
}
else
{
    // A text response
    Console.WriteLine($"Response: {response.Text}");
}
```
