# ChatSessionViewModel Integration for MCP

Instead of replacing the entire ChatSessionViewModel class, you should integrate the MCP functionality into the existing class. Here's how to modify the existing `ChatSessionViewModel.cs`:

## 1. Update the Constructor and Fields

Replace the constructor and add the necessary fields:

```csharp
// Add these fields
private readonly IToolRegistry _toolRegistry;
private bool _isProcessing = false;

// Replace the constructor
public ChatSessionViewModel(
    ILLMClientService llmClientService,
    IChatSessionService sessionService,
    IContextManager contextManager,
    IMessageParsingService messageParsingService)
{
    _llmClientService = llmClientService ?? throw new ArgumentNullException(nameof(llmClientService));
    _sessionService = sessionService ?? throw new ArgumentNullException(nameof(sessionService));
    _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
    _messageParsingService = messageParsingService ?? throw new ArgumentNullException(nameof(messageParsingService));
    
    // Try to get the tool registry from the service provider
    try
    {
        _toolRegistry = ServiceProvider.Resolve<IToolRegistry>();
    }
    catch
    {
        // Tool registry not available - MCP functionality will be limited
    }
    
    // Initialize collections
    _chatSessions = new ObservableCollection<ChatSession>();
    
    // Initialize commands
    SendCommand = new RelayCommand(async () => await SubmitInput(), CanSubmitInput);
    NewSessionCommand = new RelayCommand(async () => await CreateNewSession());
    SaveSessionCommand = new RelayCommand(async () => await SaveSession(), () => CurrentSession != null);
    DeleteSessionCommand = new RelayCommand(async () => await DeleteSession(), () => CurrentSession != null);
    AddImageCommand = new RelayCommand(ShowImagePicker);
    ToggleContextCommand = new RelayCommand(ToggleContext);
    SummarizeContextCommand = new RelayCommand(async () => await SummarizeContext());
    ClearContextCommand = new RelayCommand(ClearContext);
    SaveConversationCommand = new RelayCommand(SaveConversation);
    
    // Subscribe to events
    _messageParsingService.CodeExtracted += (s, e) => CodeExtracted?.Invoke(this, e);
    
    // Load sessions
    LoadSessionsAsync().ConfigureAwait(false);
}
```

## 2. Add IsProcessing Property

Add this property to the ChatSessionViewModel:

```csharp
public bool IsProcessing
{
    get => _isProcessing;
    set => SetProperty(ref _isProcessing, value);
}
```

## 3. Update CanSubmitInput Method

Replace the CanSubmitInput method:

```csharp
private bool CanSubmitInput()
{
    return !string.IsNullOrWhiteSpace(InputText) &&
           !string.IsNullOrWhiteSpace(SelectedModel) &&
           CurrentSession != null &&
           !IsProcessing;
}
```

## 4. Replace the SubmitInput Method

Replace the existing SubmitInput method with:

```csharp
private async Task SubmitInput()
{
    if (!CanSubmitInput()) return;
    
    IsProcessing = true;
    
    try
    {
        EnsureSessionExists();
        
        // Add user message to conversation
        string userMessage = $"User: {InputText}\n";
        AppendTextAction?.Invoke(userMessage);
        
        // Add user message to session
        CurrentSession.Messages.Add(new ChatMessage
        {
            Role = "User",
            Content = InputText,
            ImagePath = HasSelectedImage ? SelectedImagePath : null
        });
        
        // Display image if selected
        if (HasSelectedImage)
        {
            AppendImageAction?.Invoke(SelectedImagePath);
        }
        
        // Add to context
        _contextManager.AddMessage("User", InputText);
        
        // Check if the model supports MCP and we have MCP client service and tool registry
        if (_llmClientService is IMCPLLMClientService mcpService && 
            mcpService.ModelSupportsMCP(SelectedModel) && 
            _toolRegistry != null)
        {
            await ProcessWithMCP(InputText, mcpService);
        }
        else
        {
            await ProcessWithStandardGeneration(InputText);
        }
        
        // Save the session
        await _sessionService.SaveSessionAsync(CurrentSession);
        
        // Clear input and selected image
        InputText = string.Empty;
        SelectedImagePath = string.Empty;
        
        // Possibly rename the session if certain conditions are met
        if (CurrentSession.Messages.Count >= 6 &&
            CurrentSession.Name.Length < 10 &&
            (CurrentSession.Name.StartsWith("Chat ") || CurrentSession.Name.StartsWith("New ")))
        {
            await GenerateSessionNameAsync();
        }
    }
    catch (Exception ex)
    {
        // Handle error
        AppendTextAction?.Invoke($"\nError: {ex.Message}\n");
        Console.WriteLine($"Error in SubmitInput: {ex}");
    }
    finally
    {
        IsProcessing = false;
    }
}
```

## 5. Add MCP-specific Methods

Add these methods for MCP processing:

```csharp
private async Task ProcessWithMCP(string userInput, IMCPLLMClientService mcpService)
{
    try
    {
        AppendTextAction?.Invoke($"{SelectedModel}: ");
        
        // Get all available tools
        var tools = _toolRegistry.GetTools();
        
        // Get prompt with MCP context if using IMCPContextManager
        string prompt;
        if (_contextManager is IMCPContextManager mcpContextManager)
        {
            prompt = mcpContextManager.GetMCPContextualPrompt(userInput);
        }
        else
        {
            prompt = _contextManager.GetContextualPrompt(userInput);
        }
        
        // Generate response with MCP
        var mcpResponse = await mcpService.GenerateWithMCPAsync(prompt, SelectedModel, tools);
        
        if (mcpResponse.Type == "tool_use")
        {
            // Log the tool use in the context
            if (_contextManager is IMCPContextManager mcpContextManager2)
            {
                mcpContextManager2.AddToolUse(mcpResponse.Tool, mcpResponse.Input);
            }
            
            // Create a ToolUseViewModel for the UI
            var toolUseViewModel = new ToolUseViewModel
            {
                ToolName = mcpResponse.Tool,
                Input = mcpResponse.Input,
                IsExecuting = true
            };
            
            // Add any preamble text before the tool call
            if (!string.IsNullOrWhiteSpace(mcpResponse.Text))
            {
                AppendTextAction?.Invoke(mcpResponse.Text);
            }
            
            // Add a message about using the tool
            AppendTextAction?.Invoke($"\n[Using {mcpResponse.Tool} tool...]\n");
            
            // Add to the session
            CurrentSession.Messages.Add(new ChatMessage
            {
                Role = SelectedModel,
                Content = mcpResponse.Text ?? $"I'll use the {mcpResponse.Tool} tool to help with that.",
                Metadata = new Dictionary<string, object>
                {
                    { "ToolUse", toolUseViewModel },
                    { "ToolName", mcpResponse.Tool },
                    { "ToolInput", mcpResponse.Input }
                }
            });
            
            // Get the tool handler
            var toolHandler = _toolRegistry.GetToolHandler(mcpResponse.Tool);
            
            if (toolHandler != null)
            {
                try
                {
                    // Execute the tool
                    var result = await toolHandler(mcpResponse.Input);
                    
                    // Update the view model
                    toolUseViewModel.Result = result;
                    toolUseViewModel.Succeeded = true;
                    toolUseViewModel.IsExecuting = false;
                    
                    // Display tool result in the chat
                    string resultText = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    AppendTextAction?.Invoke($"\n[Tool result:]\n```json\n{resultText}\n```\n");
                    
                    // Log the result in the context
                    if (_contextManager is IMCPContextManager mcpContextManager3)
                    {
                        mcpContextManager3.AddToolResult(mcpResponse.Tool, result, true);
                    }
                    
                    // Add to the session
                    CurrentSession.Messages.Add(new ChatMessage
                    {
                        Role = "System",
                        Content = $"Tool result from {mcpResponse.Tool}:\n{resultText}",
                        Metadata = new Dictionary<string, object>
                        {
                            { "ToolResult", result },
                            { "ToolName", mcpResponse.Tool }
                        }
                    });
                    
                    // Continue the conversation with the tool result
                    await ContinueConversationWithToolResult(userInput, mcpResponse.Tool, result, mcpService);
                }
                catch (Exception ex)
                {
                    // Update the view model with the error
                    toolUseViewModel.Error = ex.Message;
                    toolUseViewModel.Succeeded = false;
                    toolUseViewModel.IsExecuting = false;
                    
                    // Display error in the chat
                    AppendTextAction?.Invoke($"\n[Tool error: {ex.Message}]\n");
                    
                    // Log the error in the context
                    if (_contextManager is IMCPContextManager mcpContextManager3)
                    {
                        mcpContextManager3.AddToolResult(mcpResponse.Tool, null, false, ex.Message);
                    }
                    
                    // Add to the session
                    CurrentSession.Messages.Add(new ChatMessage
                    {
                        Role = "System",
                        Content = $"Error executing {mcpResponse.Tool}: {ex.Message}",
                        Metadata = new Dictionary<string, object>
                        {
                            { "Error", ex.Message },
                            { "ToolName", mcpResponse.Tool }
                        }
                    });
                    
                    // Continue the conversation with the error
                    await ContinueConversationWithToolError(userInput, mcpResponse.Tool, ex.Message, mcpService);
                }
            }
            else
            {
                // Tool not found
                string errorMessage = $"Tool '{mcpResponse.Tool}' not found";
                
                // Update the view model with the error
                toolUseViewModel.Error = errorMessage;
                toolUseViewModel.Succeeded = false;
                toolUseViewModel.IsExecuting = false;
                
                // Display error in the chat
                AppendTextAction?.Invoke($"\n[{errorMessage}]\n");
                
                // Log the error in the context
                if (_contextManager is IMCPContextManager mcpContextManager3)
                {
                    mcpContextManager3.AddToolResult(mcpResponse.Tool, null, false, errorMessage);
                }
                
                // Add to the session
                CurrentSession.Messages.Add(new ChatMessage
                {
                    Role = "System",
                    Content = errorMessage,
                    Metadata = new Dictionary<string, object>
                    {
                        { "Error", errorMessage },
                        { "ToolName", mcpResponse.Tool }
                    }
                });
                
                // Continue the conversation with the error
                await ContinueConversationWithToolError(userInput, mcpResponse.Tool, errorMessage, mcpService);
            }
        }
        else if (mcpResponse.Type == "text")
        {
            // Process the text response
            await ProcessTextResponse(mcpResponse.Text);
        }
    }
    catch (Exception ex)
    {
        // Fall back to standard generation if MCP fails
        AppendTextAction?.Invoke($"\n[Error with MCP: {ex.Message}, falling back to standard generation]\n");
        await ProcessWithStandardGeneration(userInput);
    }
}

private async Task ContinueConversationWithToolResult(string originalInput, string toolName, object result, IMCPLLMClientService mcpService)
{
    try
    {
        // Generate response with the tool result
        AppendTextAction?.Invoke($"{SelectedModel}: ");
        
        var mcpResponse = await mcpService.ContinueWithToolResultAsync(
            originalInput, toolName, result, SelectedModel);
        
        if (mcpResponse.Type == "tool_use")
        {
            // We have another tool call - recursively handle it
            await ProcessWithMCP(originalInput, mcpService);
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

private async Task ContinueConversationWithToolError(string originalInput, string toolName, string error, IMCPLLMClientService mcpService)
{
    try
    {
        // Get context with the error
        string contextWithError;
        if (_contextManager is IMCPContextManager mcpContextManager)
        {
            contextWithError = mcpContextManager.GetMCPContextualPrompt(originalInput);
        }
        else
        {
            contextWithError = _contextManager.GetContextualPrompt(originalInput);
        }
        
        // Generate response acknowledging the error
        AppendTextAction?.Invoke($"{SelectedModel}: ");
        
        var response = await _llmClientService.GenerateTextResponseAsync(
            $"{contextWithError}\n\nThe tool '{toolName}' encountered an error: {error}. Please acknowledge this error and suggest alternatives.",
            SelectedModel);
        
        // Process the text response
        await ProcessTextResponse(response);
    }
    catch (Exception ex)
    {
        AppendTextAction?.Invoke($"\n[Error handling tool error: {ex.Message}]\n");
    }
}

private async Task ProcessTextResponse(string text)
{
    // Process the text response with code extraction
    _messageParsingService.ProcessMessage(
        text,
        textPart => AppendTextAction?.Invoke(textPart),
        (language, code) => HandleCodeAction?.Invoke(language, code));
    
    // Add to session
    CurrentSession.Messages.Add(new ChatMessage
    {
        Role = SelectedModel,
        Content = text
    });
    
    // Add to context
    _contextManager.AddMessage(SelectedModel, text);
}
```

## 6. Update LoadSessionContent Method

Modify the LoadSessionContent method to handle tool-related metadata:

```csharp
private void LoadSessionContent()
{
    if (CurrentSession == null) return;
    
    // Clear conversation display
    ClearConversationAction?.Invoke();
    
    // Clear context
    _contextManager.ClearContext();
    
    // Process each message
    if (CurrentSession.Messages != null && CurrentSession.Messages.Count > 0)
    {
        foreach (var message in CurrentSession.Messages)
        {
            // Add to context
            _contextManager.AddMessage(message.Role, message.Content);
            
            // Display role
            AppendTextAction?.Invoke($"{message.Role}: ");
            
            // Process message content
            _messageParsingService.ProcessMessage(
                message.Content,
                text => AppendTextAction?.Invoke(text),
                (language, code) => HandleCodeAction?.Invoke(language, code));
            
            // Display image if present
            if (!string.IsNullOrEmpty(message.ImagePath) && File.Exists(message.ImagePath))
            {
                AppendImageAction?.Invoke(message.ImagePath);
            }
            
            // Handle tool use if present in metadata
            if (message.Metadata != null && 
                message.Metadata.ContainsKey("ToolUse") && 
                message.Metadata.ContainsKey("ToolName") &&
                message.Metadata.ContainsKey("ToolInput"))
            {
                string toolName = message.Metadata["ToolName"].ToString();
                var toolInput = message.Metadata["ToolInput"] as Dictionary<string, object>;
                
                AppendTextAction?.Invoke($"\n[Used {toolName} tool with parameters:]\n");
                string inputText = System.Text.Json.JsonSerializer.Serialize(toolInput, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                AppendTextAction?.Invoke($"```json\n{inputText}\n```\n");
            }
            
            // Handle tool result if present in metadata
            if (message.Metadata != null && 
                message.Metadata.ContainsKey("ToolResult") &&
                message.Metadata.ContainsKey("ToolName"))
            {
                string toolName = message.Metadata["ToolName"].ToString();
                var toolResult = message.Metadata["ToolResult"];
                
                AppendTextAction?.Invoke($"\n[Result from {toolName} tool:]\n");
                string resultText = System.Text.Json.JsonSerializer.Serialize(toolResult, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                AppendTextAction?.Invoke($"```json\n{resultText}\n```\n");
            }
        }
    }
}
```

## 7. Add Missing Imports

Make sure to add these imports to the file:

```csharp
using AIAgentTest.Services.MCP;
using AIAgentTest.Services.Interfaces;
using System.Text.Json;
```

These changes will integrate the MCP functionality into the existing ChatSessionViewModel class without creating conflicts.
