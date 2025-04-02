# MCP Implementation for AIAgent

This guide outlines how to implement the Model Context Protocol (MCP) and tool calling capabilities into the AIAgent project.

## Overview

The implementation adds the following features:
- Support for MCP (Model Context Protocol) to enable tools/functions
- Enhanced context management for tool interactions
- Ollama adapter to provide MCP-like capabilities for Ollama models
- Built-in tools for common functions (web search, file operations, etc.)
- UI support for displaying tool usage and results

## Implementation Steps

### 1. Create Core MCP Interfaces

First, create the necessary interfaces for MCP support:

- `IMCPLLMClient`: Interface for MCP-capable LLM clients
- `IMCPContextManager`: Interface for MCP-aware context management
- `IToolRegistry`: Interface for registering and managing tools
- Enhance `IMessageParsingService` to support parsing MCP responses

### 2. Implement Core MCP Models

Create model classes for MCP:

- `ToolDefinition`: Enhanced version of FunctionDefinition for MCP
- `MCPContextMessage`: Represents messages in MCP context
- `MCPResponse`: Represents responses from MCP-enabled LLMs
- `ToolUseViewModel`: ViewModel for representing tool usage in the UI

### 3. Implement MCP Context Manager

Extend the existing `ContextManager` to support MCP-specific context:

- Tool usage tracking
- Tool results tracking
- Enhanced prompt construction with tool context
- Separate history for MCP interactions

### 4. Create Ollama MCP Adapter

Implement an adapter that provides MCP capabilities for Ollama:

- Special prompt formatting to encourage tool usage
- Response parsing to extract tool calls
- Handling of tool results and follow-up responses

### 5. Implement Tool Registry

Create a registry for managing tools:

- Tool registration with handlers
- Tool discovery and retrieval
- Conversion between function and tool formats for backward compatibility

### 6. Create Common Tools

Implement a set of common tools:

- Web search tool
- Current time tool
- File operations (read/write)
- Simple calculator

### 7. Update Service Registration

Modify the service registration to include MCP services:

- Register MCP interfaces and implementations
- Create an MCP client factory
- Register tools with the tool registry
- Connect MCP services to existing services

### 8. Update ChatSessionViewModel

Enhance the ChatSessionViewModel to support MCP and tool execution:

- Add MCP-specific processing flow
- Handle tool calls and results
- Display tool usage in the UI
- Manage tool execution and error handling

## File Structure

The MCP implementation adds the following files to the project:

### API_Clients/MCP/
- `IMCPLLMClient.cs`: Interface for MCP-capable LLM clients
- `MCPClientFactory.cs`: Factory for creating MCP clients
- `OllamaMCPAdapter.cs`: Adapter to provide MCP for Ollama

### Services/Interfaces/
- `IMCPContextManager.cs`: Interface for MCP context management
- `IMCPLLMClientService.cs`: Service interface for MCP clients

### Services/MCP/
- `CommonTools.cs`: Implementation of common tools
- `MCPContextManager.cs`: Implementation of MCP context manager
- `MCPLLMClientService.cs`: Implementation of MCP client service
- `MCPModels.cs`: Model classes for MCP
- `MCPServiceRegistration.cs`: Registration for MCP services
- `ToolRegistry.cs`: Implementation of tool registry
- `IToolRegistry.cs`: Interface for tool registry

### ViewModels/
- `ToolUseViewModel.cs`: ViewModel for tool usage

## Usage

To use the MCP implementation:

1. Initialize MCP services in App.xaml.cs:
```csharp
// Register base services
RegisterBaseServices();

// Register MCP services
MCPServiceRegistration.RegisterMCPServices();
```

2. Use the `IMCPLLMClientService` for generating responses:
```csharp
var mcpLLMService = ServiceProvider.Resolve<IMCPLLMClientService>();
var tools = ServiceProvider.Resolve<IToolRegistry>().GetTools();
var response = await mcpLLMService.GenerateWithMCPAsync(prompt, model, tools);
```

3. Process tool usage:
```csharp
if (response.Type == "tool_use")
{
    var toolHandler = toolRegistry.GetToolHandler(response.Tool);
    var result = await toolHandler(response.Input);
    var followUp = await mcpLLMService.ContinueWithToolResultAsync(prompt, response.Tool, result, model);
}
```

## Adding Custom Tools

To add a custom tool:

1. Create a method that implements the tool logic:
```csharp
private async Task<object> MyCustomToolHandler(Dictionary<string, object> parameters)
{
    // Tool implementation
    return result;
}
```

2. Register the tool with the registry:
```csharp
toolRegistry.RegisterTool(
    name: "my_custom_tool",
    description: "Description of my custom tool",
    inputSchema: new Dictionary<string, object> {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object> {
            ["param1"] = new Dictionary<string, object> {
                ["type"] = "string",
                ["description"] = "Parameter description"
            }
        },
        ["required"] = new List<string> { "param1" }
    },
    outputSchema: new Dictionary<string, object> {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object> {
            ["result"] = new Dictionary<string, object> { ["type"] = "string" }
        }
    },
    handler: MyCustomToolHandler,
    toolType: "function",
    tags: new List<string> { "Custom", "Category" }
);
```

## Testing

To test the MCP implementation:

1. Try using tools in a conversation
2. Check that tool calls are correctly parsed
3. Verify that tool results are displayed in the UI
4. Test error handling for tool execution
5. Check that context is maintained across tool interactions

## Future Improvements

Potential future enhancements:

1. Add more sophisticated tools
2. Implement tool composition (tools calling other tools)
3. Add support for other LLM providers
4. Create a tool development UI
5. Implement persistent tool history
6. Add authentication for sensitive tools
7. Implement streaming for tool results
