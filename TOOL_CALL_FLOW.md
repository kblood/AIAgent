# AIAgent Tool Call Flow and Architecture

## Introduction

This document outlines the architecture and flow of tool calls in the AIAgent application. Tool calls allow Language Models (LLMs) to interact with your system, access external resources, and perform actions that extend beyond simple text generation.

## Tool Call Architecture Overview

The tool call system in AIAgent follows a layered architecture with these main components:

1. **UI Layer** (ViewModels and Views)
2. **Tool Registry & Management Layer**
3. **MCP (Model Context Protocol) Layer**
4. **Tool Implementation Layer**
5. **Tool Execution Layer**

## Component Relationships

```
┌─────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  UI Layer   │◄───►│ Tool Management │◄───►│  MCP Layer      │
│             │     │     Layer        │     │                 │
└─────────────┘     └─────────────────┘     └────────┬────────┘
                                                     │
                                                     ▼
                                            ┌─────────────────┐
                                            │Tool Implementation│
                                            │     Layer        │
                                            └────────┬─────────┘
                                                     │
                                                     ▼
                                            ┌─────────────────┐
                                            │Tool Execution   │
                                            │     Layer       │
                                            └─────────────────┘
```

## UI Layer: Starting a Tool Call

Tool calls begin in the UI layer, primarily in `ChatSessionViewModel.cs`. Here's the flow:

1. The user submits a query via `ChatSessionViewModel.SubmitInput()`
2. If the selected model supports MCP, we process with `ProcessWithMCP()`
3. The input message is stored in the current session and added to the context

```csharp
// In ChatSessionViewModel.cs - SubmitInput() method

if (_llmClientService.ModelSupportsMCP(SelectedModel) && _toolRegistry != null)
{
    await ProcessWithMCP(InputText);
}
else
{
    await ProcessWithStandardGeneration(InputText);
}
```

## MCP Layer: Detecting Tool Calls

The MCP layer handles communication with the LLM and identifies tool calls in responses:

1. The input is preprocessed and sent to the LLM via `_llmClientService.GenerateWithMCPAsync()`
2. `OllamaMCPAdapter` is responsible for parsing the LLM's response
3. If a tool call is detected, it returns an `MCPResponse` with type "tool_use"

```csharp
// In ChatSessionViewModel.cs - ProcessWithMCP() method

var mcpResponse = await _llmClientService.GenerateWithMCPAsync(prompt, SelectedModel, tools);

if (mcpResponse.Type == "tool_use")
{
    // Log the tool use in the context and debug info
    Console.WriteLine($"Tool call detected: {mcpResponse.Tool} with input: {JsonSerializer.Serialize(mcpResponse.Input)}");
    _contextManager.AddToolUse(mcpResponse.Tool, mcpResponse.Input);
    
    // Create a ToolUseViewModel for the UI
    var toolUseViewModel = new ToolUseViewModel
    {
        ToolName = mcpResponse.Tool,
        Input = mcpResponse.Input,
        IsExecuting = true
    };
    
    // ... handle tool execution
}
```

## Tool Registry and Management

The Tool Registry is a central component that:

1. Maintains a registry of all available tools and their handlers
2. Provides tool definitions to LLMs (name, description, parameters)
3. Resolves tool handlers at execution time

The key classes involved are:
- `IToolRegistry` interface
- `ToolRegistry` implementation
- `ToolDefinition` data structure

## Tool Implementation Layer

Tool implementations are classes that contain the handlers for tool calls:

1. `CommonTools` provides implementation for common file system operations
2. Various other tool implementations exist in the `Services/MCP` folder
3. Each tool defines:
   - A schema for input parameters
   - A handler function to execute the tool
   - Metadata like name, description, etc.

Tool registration happens in the `RegisterCommonTools` method:

```csharp
public void RegisterCommonTools(IToolRegistry registry)
{
    if (registry == null)
        throw new ArgumentNullException(nameof(registry));
    
    // Register list_tools tool
    registry.RegisterTool(
        new ToolDefinition { ... },
        ListToolsHandler);
    
    // Register filesystem tools
    RegisterReadFileTool(registry);
    RegisterReadMultipleFilesTool(registry);
    RegisterWriteFileTool(registry);
    RegisterEditFileTool(registry);
    // ... etc.
}
```

## Tool Execution Flow

When a tool call is detected:

1. `ChatSessionViewModel.ProcessWithMCP()` gets the tool handler from the registry
2. The handler is executed with the input parameters
3. The result is processed and displayed in the UI
4. The result is sent back to the LLM to continue the conversation

```csharp
// In ChatSessionViewModel.cs - ProcessWithMCP() method

var toolHandler = _toolRegistry.GetToolHandler(mcpResponse.Tool);

if (toolHandler != null)
{
    try
    {
        // Execute the tool
        var result = await toolHandler(input);
        
        // Update UI
        toolUseViewModel.Result = result;
        toolUseViewModel.Succeeded = true;
        toolUseViewModel.IsExecuting = false;
        
        // Format and display result
        string resultText = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
        
        // Continue conversation with the tool result
        await ContinueConversationWithToolResult(userInput, mcpResponse.Tool, result);
    }
    catch (Exception ex)
    {
        // Handle errors
        // ...
    }
}
```

## Tool Call Response Handling

After a tool executes:

1. The result is stored in the session with metadata
2. The result is added to the context in `_contextManager.AddToolResult()`
3. The conversation continues with the tool result using `ContinueConversationWithToolResult()`
4. The LLM generates a response based on the tool result

```csharp
private async Task ContinueConversationWithToolResult(string originalInput, string toolName, object result)
{
    try
    {
        // Generate response with the tool result
        AppendTextAction?.Invoke($"{SelectedModel}: ");
        
        var mcpResponse = await _llmClientService.ContinueWithToolResultAsync(
            originalInput, toolName, result, SelectedModel);
        
        if (mcpResponse.Type == "tool_use")
        {
            // We have another tool call - recursively handle it
            await ProcessWithMCP(originalInput);
        }
        else
        {
            // Process the text response
            await ProcessTextResponse(mcpResponse.Text);
        }
    }
    catch (Exception ex)
    {
        AppendTextAction?.Invoke($"\n[Error continuing conversation: {ex.Message}]\n");
    }
}
```

## Testing Tools Directly

The application includes a dedicated tool testing UI:

1. `ToolTestingViewModel` provides a UI for testing tools
2. Users can select a tool, configure parameters, and execute it
3. Results are displayed in the UI

This is useful for debugging tool functionality without going through the LLM.

## Debug and Visualization

Tool calls and their results are visualized in the UI:

1. The Debug panel shows tool calls and their results
2. Tool call JSON is displayed in code blocks
3. Tool results are formatted and displayed in the chat

The `DebugViewModel` and its corresponding view show technical details about tool calls:

```csharp
// In DebugViewModel.cs - ShowTechnicalContext() method

// Tool call code
if (message.Metadata.ContainsKey("ToolCallJson"))
{
    string toolName = message.Metadata.ContainsKey("ToolName") 
        ? message.Metadata["ToolName"].ToString()
        : "unknown";
        
    contextBuilder.AppendLine($"--- TOOL CALL: {toolName} ---");
    contextBuilder.AppendLine(message.Metadata["ToolCallJson"].ToString());
}

// Tool result code
if (message.Metadata.ContainsKey("ToolResultJson"))
{
    string toolName = message.Metadata.ContainsKey("ToolName") 
        ? message.Metadata["ToolName"].ToString()
        : "unknown";
        
    contextBuilder.AppendLine($"--- TOOL RESULT: {toolName} ---");
    contextBuilder.AppendLine(message.Metadata["ToolResultJson"].ToString());
}
```

## Error Handling

Tool execution errors are handled at multiple levels:

1. At the tool execution level, errors are caught and returned in a structured format
2. At the UI level, errors are displayed to the user and logged
3. The conversation continues with error information provided to the LLM

This ensures robustness and allows the LLM to recover from tool errors.

## Adding New Tools

To add a new tool to the system:

1. Define a new tool handler method in a suitable class
2. Create a tool registration method similar to existing ones
3. Register the tool with the registry in your initialization code
4. Ensure appropriate error handling and result formatting

Example of registering a new tool:

```csharp
private void RegisterMyNewTool(IToolRegistry registry)
{
    var toolDefinition = new ToolDefinition
    {
        Name = "my_new_tool",
        Description = "Description of what my new tool does",
        Schema = JsonSerializer.Serialize(new
        {
            type = "object",
            properties = new
            {
                parameter1 = new
                {
                    type = "string",
                    description = "Description of parameter1"
                },
                // Additional parameters...
            },
            required = new[] { "parameter1" },
            additionalProperties = false
        }),
        Tags = new[] { "My Category" },
        Metadata = new Dictionary<string, object>
        {
            { "friendly_name", "My New Tool" },
            { "priority", 50 }
        }
    };

    registry.RegisterTool(toolDefinition, MyNewToolHandler);
}

private async Task<object> MyNewToolHandler(object input)
{
    // Implementation of the tool
    // ...
}
```

## Conclusion

The tool call system in AIAgent provides a powerful mechanism for extending LLM capabilities. It follows a well-structured architecture that separates concerns and allows for extensibility and testing. By understanding this flow, you can effectively debug, extend, and optimize the tool call system in your application.