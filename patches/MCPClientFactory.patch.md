# MCPClientFactory Patch

Here are the changes needed to update the `MCPClientFactory` class to support the enhanced Ollama MCP adapter.

## Fields to Add

```csharp
private readonly IToolRegistry _toolRegistry;
private readonly Dictionary<string, IMCPServerClient> _serverClients = new Dictionary<string, IMCPServerClient>();
```

## Constructor to Update

```csharp
/// <summary>
/// Creates a new MCPClientFactory
/// </summary>
/// <param name="llmClientFactory">Factory for creating base LLM clients</param>
/// <param name="parsingService">Service for parsing messages</param>
/// <param name="toolRegistry">Registry of available tools</param>
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

## CreateMCPClient Method to Update

```csharp
/// <summary>
/// Creates an MCP-capable client for the specified provider
/// </summary>
/// <param name="provider">Provider name (e.g., "ollama", "openai")</param>
/// <returns>MCP-capable LLM client</returns>
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
            
            // Use the enhanced adapter if tool registry is available, otherwise use the basic adapter
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

## GetClientForModel Method to Update

```csharp
/// <summary>
/// Gets an MCP-capable client for the specified model
/// </summary>
/// <param name="modelName">Model name</param>
/// <returns>MCP-capable LLM client</returns>
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

## Methods to Add

```csharp
/// <summary>
/// Registers an MCP server with the factory
/// </summary>
/// <param name="serverName">Name of the server</param>
/// <param name="serverClient">Client for communicating with the server</param>
public void RegisterMCPServer(string serverName, IMCPServerClient serverClient)
{
    _serverClients[serverName] = serverClient;
}

/// <summary>
/// Gets all registered MCP servers
/// </summary>
public Dictionary<string, IMCPServerClient> GetMCPServers()
{
    return new Dictionary<string, IMCPServerClient>(_serverClients);
}

/// <summary>
/// Gets an MCP server by name
/// </summary>
/// <param name="serverName">Name of the server</param>
/// <returns>MCP server client or null if not found</returns>
public IMCPServerClient GetMCPServer(string serverName)
{
    return _serverClients.TryGetValue(serverName, out var server) ? server : null;
}
```
