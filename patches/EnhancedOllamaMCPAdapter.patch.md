# EnhancedOllamaMCPAdapter Fixes

When implementing the `EnhancedOllamaMCPAdapter.cs` file, make the following changes to avoid compilation errors:

1. Remove the `IMCPServerClient` interface definition, since it's now defined in a separate file.

2. Adjust line 361 from:
```csharp
/// <summary>
/// Interface for MCP server clients
/// </summary>
public interface IMCPServerClient
{
    /// <summary>
    /// Gets the available tools from the server
    /// </summary>
    Task<List<ToolDefinition>> GetAvailableToolsAsync();
    
    /// <summary>
    /// Executes a tool on the server
    /// </summary>
    Task<object> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters);
    
    /// <summary>
    /// Checks if the server is available
    /// </summary>
    Task<bool> IsAvailableAsync();
}
```

To simply include the using statement:
```csharp
// IMCPServerClient is now defined in a separate file
```

This will ensure that the code compiles correctly.
