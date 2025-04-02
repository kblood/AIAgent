using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using AIAgentTest.Commands;
using AIAgentTest.Models;
using AIAgentTest.Services.Interfaces;
using System.Linq;
using System.IO;
using System.Text;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.ViewModels
{
    public class ChatSessionViewModel : ViewModelBase
    {
        private readonly IMCPLLMClientService _llmClientService;
        private readonly IChatSessionService _sessionService;
        private readonly IMCPContextManager _contextManager;
        private readonly IMessageParsingService _messageParsingService;
        private readonly IToolRegistry _toolRegistry;
        
        private ObservableCollection<ChatSession> _chatSessions;
        private ChatSession _currentSession;
        private string _inputText;
        private string _selectedImagePath;
        private string _selectedModel;
        private bool _isContextEnabled = true;
        private bool _isProcessing = false;

        // Events
        public event EventHandler<CodeExtractedEventArgs> CodeExtracted;
        
        // Properties
        public ObservableCollection<ChatSession> ChatSessions
        {
            get => _chatSessions;
            set => SetProperty(ref _chatSessions, value);
        }
        
        public ChatSession CurrentSession
        {
            get => _currentSession;
            set
            {
                if (SetProperty(ref _currentSession, value))
                {
                    LoadSessionContent();
                }
            }
        }
        
        public string InputText
        {
            get => _inputText;
            set => SetProperty(ref _inputText, value);
        }
        
        public string SelectedImagePath
        {
            get => _selectedImagePath;
            set => SetProperty(ref _selectedImagePath, value);
        }
        
        public string SelectedModel
        {
            get => _selectedModel;
            set => SetProperty(ref _selectedModel, value);
        }
        
        public bool IsContextEnabled
        {
            get => _isContextEnabled;
            set
            {
                if (SetProperty(ref _isContextEnabled, value))
                {
                    _contextManager.IsContextEnabled = value;
                }
            }
        }
        
        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }
        
        public bool HasSelectedImage => !string.IsNullOrEmpty(SelectedImagePath) && File.Exists(SelectedImagePath);
        
        // Commands
        public ICommand SendCommand { get; }
        public ICommand NewSessionCommand { get; }
        public ICommand SaveSessionCommand { get; }
        public ICommand DeleteSessionCommand { get; }
        public ICommand AddImageCommand { get; }
        public ICommand ToggleContextCommand { get; }
        public ICommand SummarizeContextCommand { get; }
        public ICommand ClearContextCommand { get; }
        public ICommand SaveConversationCommand { get; }
        
        // Methods for interacting with the UI
        public Action<string> AppendTextAction { get; set; }
        public Action<string, string> HandleCodeAction { get; set; }
        public Action<string> AppendImageAction { get; set; }
        public Action ClearConversationAction { get; set; }
        
        // Constructor for dependency injection
        public ChatSessionViewModel(
            IMCPLLMClientService llmClientService,
            IChatSessionService sessionService,
            IMCPContextManager contextManager,
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
        
        private bool CanSubmitInput()
        {
            return !string.IsNullOrWhiteSpace(InputText) &&
                   !string.IsNullOrWhiteSpace(SelectedModel) &&
                   CurrentSession != null &&
                   !IsProcessing;
        }
        
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
                
                // Check if the model supports MCP
                if (_llmClientService.ModelSupportsMCP(SelectedModel) && _toolRegistry != null)
                {
                    await ProcessWithMCP(InputText);
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
        
        private async Task ProcessWithMCP(string userInput)
        {
            try
            {
                AppendTextAction?.Invoke($"{SelectedModel}: ");
                
                // Get all available tools
                var tools = _toolRegistry.GetTools();
                
                // Get prompt with MCP context
                var prompt = _contextManager.GetMCPContextualPrompt(userInput);
                
                // Generate response with MCP
                var mcpResponse = await _llmClientService.GenerateWithMCPAsync(prompt, SelectedModel, tools);
                
                if (mcpResponse.Type == "tool_use")
                {
                    // Log the tool use in the context
                    _contextManager.AddToolUse(mcpResponse.Tool, mcpResponse.Input);
                    
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
                            string resultText = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
                            AppendTextAction?.Invoke($"\n[Tool result:]\n```json\n{resultText}\n```\n");
                            
                            // Log the result in the context
                            _contextManager.AddToolResult(mcpResponse.Tool, result, true);
                            
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
                            await ContinueConversationWithToolResult(userInput, mcpResponse.Tool, result);
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
                            _contextManager.AddToolResult(mcpResponse.Tool, null, false, ex.Message);
                            
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
                            await ContinueConversationWithToolError(userInput, mcpResponse.Tool, ex.Message);
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
                        _contextManager.AddToolResult(mcpResponse.Tool, null, false, errorMessage);
                        
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
                        await ContinueConversationWithToolError(userInput, mcpResponse.Tool, errorMessage);
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
        
        private async Task ContinueConversationWithToolError(string originalInput, string toolName, string error)
        {
            try
            {
                // Get context with the error
                var contextWithError = _contextManager.GetMCPContextualPrompt(originalInput);
                
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
        
        private async Task ProcessWithStandardGeneration(string userInput)
        {
            try
            {
                // Text-only response
                AppendTextAction?.Invoke($"{SelectedModel}: ");
                
                // Get contextual prompt if enabled
                var prompt = _contextManager.IsContextEnabled
                    ? _contextManager.GetContextualPrompt(userInput)
                    : userInput;
                
                string fullResponse;
                
                // Handle image-based generation
                if (HasSelectedImage)
                {
                    // Generate response with image
                    fullResponse = await _llmClientService.GenerateResponseWithImageAsync(
                        prompt, 
                        SelectedImagePath, 
                        SelectedModel);
                    
                    // Process the response
                    _messageParsingService.ProcessMessage(
                        fullResponse,
                        text => AppendTextAction?.Invoke(text),
                        (language, code) => HandleCodeAction?.Invoke(language, code));
                }
                else
                {
                    // Process streamed response
                    var responseBuilder = new StringBuilder();
                    await foreach (var chunk in _llmClientService.GenerateStreamResponseAsync(prompt, SelectedModel))
                    {
                        AppendTextAction?.Invoke(chunk);
                        responseBuilder.Append(chunk);
                    }
                    
                    fullResponse = responseBuilder.ToString();
                    
                    // Process code blocks after streaming completes
                    var codeBlocks = _messageParsingService.ExtractCodeBlocks(fullResponse);
                    foreach (var block in codeBlocks)
                    {
                        if (block.Language != null && block.Code != null)
                        {
                            HandleCodeAction?.Invoke(block.Language, block.Code);
                        }
                    }
                }
                
                // Add assistant message to session
                CurrentSession.Messages.Add(new ChatMessage
                {
                    Role = SelectedModel,
                    Content = fullResponse,
                    ImagePath = HasSelectedImage ? SelectedImagePath : null
                });
                
                // Update context with new messages
                _contextManager.AddMessage(SelectedModel, fullResponse);
            }
            catch (Exception ex)
            {
                AppendTextAction?.Invoke($"\n[Error: {ex.Message}]\n");
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
        
        private async Task LoadSessionsAsync()
        {
            var sessions = await _sessionService.LoadSessionsAsync();
            ChatSessions.Clear();
            foreach (var session in sessions)
            {
                ChatSessions.Add(session);
            }
            
            if (ChatSessions.Count > 0)
            {
                CurrentSession = ChatSessions[0];
            }
        }
        
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
                        string inputText = JsonSerializer.Serialize(toolInput, new JsonSerializerOptions { WriteIndented = true });
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
                        string resultText = JsonSerializer.Serialize(toolResult, new JsonSerializerOptions { WriteIndented = true });
                        AppendTextAction?.Invoke($"```json\n{resultText}\n```\n");
                    }
                }
            }
        }
        
        private async Task CreateNewSession(string name = null)
        {
            var sessionName = name ?? $"Chat {ChatSessions.Count + 1}";
            
            var session = new ChatSession
            {
                Name = sessionName,
                ModelName = SelectedModel
            };
            
            ChatSessions.Add(session);
            CurrentSession = session;
            
            await _sessionService.SaveSessionAsync(session);
            _contextManager.ClearContext();
        }
        
        private async Task SaveSession()
        {
            if (CurrentSession != null)
            {
                await _sessionService.SaveSessionAsync(CurrentSession);
            }
        }
        
        private async Task DeleteSession()
        {
            if (CurrentSession != null)
            {
                await _sessionService.DeleteSessionAsync(CurrentSession);
                ChatSessions.Remove(CurrentSession);
                
                if (ChatSessions.Count == 0)
                {
                    await CreateNewSession("New Chat");
                }
                else
                {
                    CurrentSession = ChatSessions[0];
                }
            }
        }
        
        private void ShowImagePicker()
        {
            // This will be implemented in the view code through a callback
        }
        
        private void ToggleContext()
        {
            IsContextEnabled = !IsContextEnabled;
            
            if (IsContextEnabled && CurrentSession != null)
            {
                _contextManager.ClearContext();
                foreach (var msg in CurrentSession.Messages)
                {
                    _contextManager.AddMessage(msg.Role, msg.Content);
                }
            }
            else
            {
                _contextManager.ClearContext();
            }
        }
        
        private async Task SummarizeContext()
        {
            try
            {
                AppendTextAction?.Invoke("\n[System: Summarizing context...]\n");
                await _contextManager.SummarizeContext(SelectedModel);
                AppendTextAction?.Invoke("[System: Context has been summarized.]\n");
            }
            catch (Exception ex)
            {
                AppendTextAction?.Invoke($"\n[Error summarizing context: {ex.Message}]\n");
            }
        }
        
        private void ClearContext()
        {
            _contextManager.ClearContext();
            AppendTextAction?.Invoke("\n[System: Context has been cleared.]\n");
        }
        
        private void SaveConversation()
        {
            // This will be implemented in the view code through a callback
        }
        
        private void EnsureSessionExists()
        {
            if (CurrentSession == null)
            {
                CreateNewSession().ConfigureAwait(false);
            }
        }
        
        private async Task GenerateSessionNameAsync()
        {
            try
            {
                // Create a prompt for the model to generate a session name
                var namePrompt = "Based on this conversation, generate a concise and descriptive name (maximum 30 characters) that captures the main topic or purpose. Respond with only the name, no explanations or quotes:\n\n";

                // Add the conversation content to the prompt
                foreach (var message in CurrentSession.Messages)
                {
                    namePrompt += $"{message.Role}: {message.Content}\n";
                }

                // Get the generated name from the model
                var generatedName = await _llmClientService.GenerateTextResponseAsync(namePrompt, SelectedModel);

                // Clean up and truncate the name
                generatedName = generatedName.Trim().TrimEnd('.').TrimEnd();
                if (generatedName.Length > 30)
                {
                    generatedName = generatedName.Substring(0, 27) + "...";
                }

                // Update the session name
                CurrentSession.Name = generatedName;
                OnPropertyChanged(nameof(CurrentSession));

                // Save the session with the new name
                await _sessionService.SaveSessionAsync(CurrentSession);
            }
            catch (Exception ex)
            {
                // Log the error but don't disrupt the chat flow
                Console.WriteLine($"Error generating session name: {ex.Message}");
            }
        }
    }
}