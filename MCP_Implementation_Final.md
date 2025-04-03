# Enhanced MCP Implementation - Final Instructions

This document provides the final implementation instructions for the enhanced MCP capabilities in the AIAgent project.

## Implementation Steps

Follow these steps in order to implement the enhanced MCP functionality without causing compilation errors:

### Step 1: Add the IMCPServerClient Interface

First, create the `IMCPServerClient.cs` file in the `AIAgentTest\API_Clients\MCP` directory.
- File created: `AIAgentTest\API_Clients\MCP\IMCPServerClient.cs`

### Step 2: Add the EnhancedOllamaMCPAdapter

Create the `EnhancedOllamaMCPAdapter.cs` file in the `AIAgentTest\API_Clients\MCP` directory.
- File created: `AIAgentTest\API_Clients\MCP\EnhancedOllamaMCPAdapter.cs` 
- Make sure to **remove** the `IMCPServerClient` interface definition from this file, as it's now in a separate file.

### Step 3: Add the FileSystemMCPServerClient

Create the `FileSystemMCPServerClient.cs` file in the `AIAgentTest\API_Clients\MCP` directory.
- File created: `AIAgentTest\API_Clients\MCP\FileSystemMCPServerClient.cs`

### Step 4: Add the MCPServerRegistration

Create the `MCPServerRegistration.cs` file in the `AIAgentTest\Services\MCP` directory.
- File created: `AIAgentTest\Services\MCP\MCPServerRegistration.cs`

### Step 5: Update the MCPClientFactory

Follow these steps to update the `MCPClientFactory.cs` file:

1. Add the new fields:
```csharp
private readonly IToolRegistry _toolRegistry;
private readonly Dictionary<string, IMCPServerClient> _serverClients = new Dictionary<string, IMCPServerClient>();
```

2. Update the constructor to accept the tool registry:
```csharp
public MCPClientFactory(
    LLMClientFactory llmClientFactory,
    IMessageParsingService parsingService,
    IToolRegistry toolRegistry = null)
{
    _llmClientFactory = llmClientFactory ?? throw new ArgumentNullException(nameof(llmClientFactory));
    _parsingService = parsingService ?? throw new ArgumentNullException(nameof(parsingService));
    _toolRegistry = toolRegistry ?? ServiceProvider.GetService<IToolRegistry>();
}
```

3. Update the `CreateMCPClient` method to use the enhanced adapter:
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

4. Update the `GetClientForModel` method to include more models:
```csharp
public IMCPLLMClient GetClientForModel(string modelName)
{
    // Simple provider detection based on model name prefixes
    if (modelName.StartsWith("llama") || 
        modelName.StartsWith("mistral") || 
        modelName.StartsWith("mixtral") ||
        modelName.StartsWith("phi") ||
        modelName.StartsWith("gemma") ||
        modelName.StartsWith("qwen"))
    {
        return CreateMCPClient("ollama");
    }
    
    // Add other model detection as needed
    
    throw new NotSupportedException($"Model {modelName} is not supported for MCP");
}
```

5. Add methods for managing MCP servers:
```csharp
public void RegisterMCPServer(string serverName, IMCPServerClient serverClient)
{
    _serverClients[serverName] = serverClient;
}

public Dictionary<string, IMCPServerClient> GetMCPServers()
{
    return new Dictionary<string, IMCPServerClient>(_serverClients);
}

public IMCPServerClient GetMCPServer(string serverName)
{
    return _serverClients.TryGetValue(serverName, out var server) ? server : null;
}
```

### Step 6: Update the MCPServiceRegistration

Add a method for creating the enhanced adapter:
```csharp
public static EnhancedOllamaMCPAdapter CreateEnhancedOllamaAdapter()
{
    var ollamaClient = (OllamaClient)LLMClientFactory.GetClient(LLMClientFactory.ProviderType.Ollama);
    var messageParsingService = ServiceProvider.GetService<IMessageParsingService>();
    var toolRegistry = ServiceProvider.GetService<IToolRegistry>();
    
    return new EnhancedOllamaMCPAdapter(ollamaClient, messageParsingService, toolRegistry);
}
```

And update the `RegisterMCPServices` method to add server registration:
```csharp
// At the end of the RegisterMCPServices method, before the closing brace:
// Register MCP servers
MCPServerRegistration.RegisterMCPServersAsync(mcpClientFactory).ConfigureAwait(false);
```

## Testing the Implementation

After implementing these changes, you can test the enhanced MCP functionality:

1. **Create an instance of the enhanced adapter**:
```csharp
var enhancedAdapter = MCPServiceRegistration.CreateEnhancedOllamaAdapter();
```

2. **Register an external MCP server**:
```csharp
var mcpClientFactory = ServiceProvider.GetService<MCPClientFactory>();
var fileSystemServer = new FileSystemMCPServerClient("http://localhost:3000");
mcpClientFactory.RegisterMCPServer("filesystem", fileSystemServer);
```

3. **Check if a model supports MCP**:
```csharp
var llmClientService = ServiceProvider.GetService<IMCPLLMClientService>();
bool supportsMCP = llmClientService.ModelSupportsMCP("llama3:instruct");
```

4. **Generate a response with MCP tools**:
```csharp
var toolRegistry = ServiceProvider.GetService<IToolRegistry>();
var mcpLLMClientService = ServiceProvider.GetService<IMCPLLMClientService>();
var response = await mcpLLMClientService.GenerateWithMCPAsync(
    "What is the square root of 144?", 
    "llama3:instruct", 
    toolRegistry.GetTools());
```

## Troubleshooting

If you encounter compilation errors:

1. **Check for duplicate interface definitions**
   Make sure you're not defining the IMCPServerClient interface in multiple places.

2. **Verify namespace references**
   Ensure all necessary namespaces are included in each file.

3. **Ensure constructor parameters match**
   The MCPClientFactory constructor should accept a tool registry parameter.

4. **Check method signatures**
   Ensure that method signatures in the enhanced adapter match those in the IMCPLLMClient interface.

## Additional Resources

For more information on using the enhanced MCP implementation, see:

- [MCP_Enhancement_Readme.md](MCP_Enhancement_Readme.md)
- [Usage_Example.md](Usage_Example.md)
