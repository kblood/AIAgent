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

namespace AIAgentTest.ViewModels
{
    public class ChatSessionViewModel : ViewModelBase
    {
        private readonly ILLMClientService _llmClientService;
        private readonly IChatSessionService _sessionService;
        private readonly IContextManager _contextManager;
        private readonly IMessageParsingService _messageParsingService;
        
        private ObservableCollection<ChatSession> _chatSessions;
        private ChatSession _currentSession;
        private string _inputText;
        private string _selectedImagePath;
        private string _selectedModel;
        private bool _isContextEnabled = true;

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
                   CurrentSession != null;
        }
        
        private async Task SubmitInput()
        {
            if (!CanSubmitInput()) return;
            
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
                
                string fullResponse = "";
                
                // Handle image-based generation
                if (HasSelectedImage)
                {
                    AppendTextAction?.Invoke($"{SelectedModel}: ");
                    
                    // Generate response with image
                    fullResponse = await _llmClientService.GenerateResponseWithImageAsync(
                        InputText, 
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
                    // Text-only response
                    AppendTextAction?.Invoke($"{SelectedModel}: ");
                    
                    // Get contextual prompt if enabled
                    var prompt = _contextManager.IsContextEnabled
                        ? _contextManager.GetContextualPrompt(InputText)
                        : InputText;
                    
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
                _contextManager.AddMessage("User", InputText);
                _contextManager.AddMessage(SelectedModel, fullResponse);
                
                // Possibly rename the session if certain conditions are met
                if (CurrentSession.Messages.Count >= 6 &&
                    CurrentSession.Name.Length < 10 &&
                    (CurrentSession.Name.StartsWith("Chat ") || CurrentSession.Name.StartsWith("New ")))
                {
                    await GenerateSessionNameAsync();
                }
                
                // Save the session
                await _sessionService.SaveSessionAsync(CurrentSession);
                
                // Clear input
                InputText = string.Empty;
            }
            catch (Exception ex)
            {
                // Handle error
                AppendTextAction?.Invoke($"\nError: {ex.Message}\n");
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
    }
}