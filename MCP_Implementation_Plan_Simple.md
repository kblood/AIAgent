# MCP Enhancement Implementation Plan - Simplified

This document provides a simplified plan for implementing the enhanced MCP capabilities in the AIAgent project.

## Introduction

The goal is to enhance the existing Model Context Protocol (MCP) implementation to provide better tool calling capabilities with Ollama models and support for external MCP servers. To avoid compilation errors and make the implementation more straightforward, we'll take a phased approach.

## Phase 1: Implement the Core Components

1. **Create the IMCPServerClient Interface**

Create a new file `IMCPServerClient.cs` in the `AIAgentTest\API_Clients\MCP` directory:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.API_Clients.MCP
{
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
}
```

2. **Create the EnhancedOllamaMCPAdapter**

Create a new file `EnhancedOllamaMCPAdapter.cs` in the `AIAgentTest\API_Clients\MCP` directory using the content from the file we already created.

3. **Create the FileSystemMCPServerClient**

Create a new file `FileSystemMCPServerClient.cs` in the `AIAgentTest\API_Clients\MCP` directory using the content from the file we already created.

## Phase 2: Update MCPClientFactory

Update the `MCPClientFactory.cs` file, step by step:

1. First, add the new field:
```csharp
private readonly IToolRegistry _toolRegistry;
```

2. Then update the constructor:
```csharp
public MCPClientFactory(
    LLMClientFactory llmClientFactory,
    IMessageParsingService parsingService,
    IToolRegistry toolRegistry = null)
{
    _llmClientFactory = llmClientFactory ?? throw new ArgumentNullException(nameof(llmClientFactory));
    _parsingService = parsingService ?? throw new ArgumentNullException(nameof(parsingService));
    _toolRegistry = toolRegistry;
}
```

3. Create a new method to register MCP servers:
```csharp
private readonly Dictionary<string, IMCPServerClient> _serverClients = new Dictionary<string, IMCPServerClient>();

public void RegisterMCPServer(string serverName, IMCPServerClient serverClient)
{
    _serverClients[serverName] = serverClient;
}

public IMCPServerClient GetMCPServer(string serverName)
{
    return _serverClients.TryGetValue(serverName, out var server) ? server : null;
}
```

4. Finally, modify the CreateMCPClient method to use the enhanced adapter if a tool registry is available:
```csharp
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
            
            // Use the enhanced adapter if tool registry is available
            if (_toolRegistry != null)
            {
                var enhancedAdapter = new EnhancedOllamaMCPAdapter(ollamaClient, _parsingService, _toolRegistry);
                
                // Register any existing MCP servers
                foreach (var entry in _serverClients)
                {
                    enhancedAdapter.RegisterMCPServer(entry.Key, entry.Value);
                }
                
                return enhancedAdapter;
            }
            else
            {
                return new OllamaMCPAdapter(ollamaClient, _parsingService);
            }
            
        // Add other providers as needed
            
        default:
            throw new NotSupportedException($"Provider {provider} is not supported for MCP");
    }
}
```

## Phase 3: Create MCPServerRegistration

Create a new file `MCPServerRegistration.cs` in the `AIAgentTest\Services\MCP` directory using the content from the file we already created.

## Phase 4: Update MCPServiceRegistration

Add a new method to the `MCPServiceRegistration.cs` file:

```csharp
/// <summary>
/// Creates an enhanced Ollama MCP adapter
/// </summary>
public static EnhancedOllamaMCPAdapter CreateEnhancedOllamaAdapter()
{
    var ollamaClient = (OllamaClient)LLMClientFactory.GetClient(LLMClientFactory.ProviderType.Ollama);
    var messageParsingService = ServiceProvider.GetService<IMessageParsingService>();
    var toolRegistry = ServiceProvider.GetService<IToolRegistry>();
    
    return new EnhancedOllamaMCPAdapter(ollamaClient, messageParsingService, toolRegistry);
}
```

And update the `RegisterMCPServices` method to register MCP servers:

```csharp
// At the end of the RegisterMCPServices method
// Register MCP servers
MCPServerRegistration.RegisterMCPServersAsync(mcpClientFactory).ConfigureAwait(false);
```

## Conclusion

By following this phased approach, you can implement the enhanced MCP capabilities without causing compilation errors or disrupting the existing functionality. The implementation can be done step by step, testing after each phase to ensure everything is working correctly.
