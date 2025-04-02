# AIAgent MCP Implementation Plan

This document outlines the step-by-step plan for implementing the Model Context Protocol (MCP) in the AIAgent project.

## Step 1: Add Core Files

First, add the following core files to the project:

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
- `MCPModels.cs`: Model classes for MCP
- `MCPLLMClientService.cs`: Implementation of MCP client service
- `MCPServiceRegistration.cs`: Registration for MCP services
- `ToolRegistry.cs`: Implementation of tool registry
- `IToolRegistry.cs`: Interface for tool registry

### ViewModels/
- `ToolUseViewModel.cs`: ViewModel for tool usage

## Step 2: Update Service Provider

Replace the existing `ServiceProvider.cs` with the new implementation that supports generic type registration:

1. Copy `ServiceProvider.cs.new` to `ServiceProvider.cs`
2. Update references to use the new registration methods

## Step 3: Create a New App.xaml.cs with MCP Support

1. Copy `App.xaml.cs.new` to `App.xaml.cs` and understand the changes
2. Update the app startup to register both base and MCP services
3. Make sure to properly initialize MCP service dependencies

## Step 4: Update Message Parsing Service

Enhance `MessageParsingService.cs` to support MCP response parsing:

1. Update the `IMessageParsingService` interface to include MCP methods
2. Implement the new methods in `MessageParsingService`

## Step 5: Update ChatSessionViewModel

Instead of replacing the file, integrate the MCP functionality:

1. Follow the instructions in `MCP_ChatSessionViewModel_Integration.md`
2. Add the necessary fields, properties, and methods
3. Modify the existing methods to support MCP

## Step 6: Update the Models

Enhance the `ChatMessage` model to support tool metadata:

1. Make sure `ChatMessage.cs` includes a `Metadata` property
2. If not present, add it as a `Dictionary<string, object>`

## Step 7: Register the MCP Services

In `App.xaml.cs`, add the registration for MCP services:

```csharp
// Register MCP services
MCPServiceRegistration.RegisterMCPServices();
```

## Step 8: Verify System for Testing

Before testing, make sure:

1. The `ChatSessionViewModel` is properly updated
2. All MCP interfaces and classes are added
3. The services are registered correctly
4. The models are updated to support metadata

## Step 9: Test the Implementation

Test the MCP implementation with these scenarios:

1. Try using the calculator tool with a simple math problem
2. Test the current time tool
3. Verify that tool results are displayed in the UI
4. Verify that the model can respond to tool results
5. Test error handling for tool execution

## Step 10: Add Your Own Tools

After verifying the implementation, add your own custom tools:

1. Create a new class for your tools
2. Implement tool handlers for your specific use cases
3. Register them with the tool registry
4. Test your new tools in conversation

## Troubleshooting

If you encounter issues:

1. Check the service registration to ensure all dependencies are available
2. Verify that the MCP adapter is properly converting between formats
3. Make sure the ChatSessionViewModel is correctly detecting MCP support
4. Check for proper error handling in tool execution
5. Verify that context is being maintained across tool interactions

## Conclusion

Following this plan will integrate the MCP functionality into the AIAgent project, enabling powerful tool calling capabilities with a clean architecture that follows the existing MVVM pattern.
