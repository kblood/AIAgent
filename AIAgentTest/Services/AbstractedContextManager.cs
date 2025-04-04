using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System.Linq;
using AIAgentTest.API_Clients;
using AIAgentTest.Services.Interfaces;

namespace AIAgentTest.Services
{
    // ChatMessage class is already defined in ContextManager.cs

    /// <summary>
    /// Manages conversation context including history and summarization
    /// </summary>
    public class AbstractedContextManager : IContextManager
    {
        private List<ChatMessage> _chatHistory;
        private string _summarizedContext;
        private readonly ILLMClient _llmClient;
        private int _maxTokensBeforeSummarize = 2000;
        private bool _isContextEnabled = true;
        private string _defaultModel;

        /// <summary>
        /// Gets or sets whether context management is enabled
        /// </summary>
        public bool IsContextEnabled
        {
            get => _isContextEnabled;
            set => _isContextEnabled = value;
        }

        /// <summary>
        /// Gets or sets the default model to use for context operations
        /// </summary>
        public string DefaultModel
        {
            get => _defaultModel;
            set => _defaultModel = value;
        }

        /// <summary>
        /// Initializes a new instance of the AbstractedContextManager
        /// </summary>
        /// <param name="llmClient">The LLM client to use for operations</param>
        /// <param name="defaultModel">The default model to use (optional)</param>
        public AbstractedContextManager(ILLMClient llmClient, string defaultModel = "llama3")
        {
            _chatHistory = new List<ChatMessage>();
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _defaultModel = defaultModel;
            _isContextEnabled = true;
        }

        /// <summary>
        /// Adds a message to the chat history
        /// </summary>
        public void AddMessage(string role, string content)
        {
            _chatHistory.Add(new ChatMessage(role, content));
        }

        /// <summary>
        /// Gets the timestamp of the last message
        /// </summary>
        public DateTime GetLastMessageTimestamp()
        {
            if (_chatHistory.Count == 0)
                return DateTime.MinValue;

            return _chatHistory[^1].Timestamp;
        }

        /// <summary>
        /// Gets a prompt enhanced with context
        /// </summary>
        public string GetContextualPrompt(string input)
        {
            if (!IsContextEnabled)
                return input;

            var contextBuilder = new StringBuilder();
    
            if (!string.IsNullOrEmpty(_summarizedContext))
                contextBuilder.AppendLine($"Previous context summary: {_summarizedContext}");
    
            var recentMessages = _chatHistory.TakeLast(5);
            foreach (var msg in recentMessages)
            {
                contextBuilder.AppendLine($"{msg.Role}: {msg.Content}");
            }
    
            contextBuilder.AppendLine($"User: {input}");
    
            return contextBuilder.ToString();
        }

        /// <summary>
        /// Determines if the context should be summarized
        /// </summary>
        public async Task<bool> ShouldSummarize()
        {
            return _chatHistory.Count > 0 && GetCurrentContextTokens() > _maxTokensBeforeSummarize;
        }

        /// <summary>
        /// Gets the estimated token count of the current context
        /// </summary>
        private int GetCurrentContextTokens()
        {
            // Rough estimation: 1 token ≈ 4 characters
            int totalCharacters = _chatHistory.Sum(msg => msg.Content.Length);
            return totalCharacters / 4;
        }

        /// <summary>
        /// Summarizes the current context
        /// </summary>
        public async Task SummarizeContext(string modelName)
        {
            if (_chatHistory.Count == 0)
                return;

            // Use the provided model name, fall back to default model if not provided
            string modelToUse = string.IsNullOrEmpty(modelName) ? _defaultModel : modelName;
            
            // If still null, try to get a model from the client
            if (string.IsNullOrEmpty(modelToUse))
            {
                try
                {
                    // Try to get default model from client
                    var models = await _llmClient.GetAvailableModelsAsync();
                    modelToUse = models.FirstOrDefault();
                    System.Diagnostics.Debug.WriteLine($"Using first available model: {modelToUse}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error getting models: {ex.Message}");
                    throw new InvalidOperationException("No model was specified for summarization and no default models could be found.", ex);
                }
            }

            System.Diagnostics.Debug.WriteLine($"Summarizing context using model: {modelToUse ?? "(none)"}. Messages: {_chatHistory.Count}");

            string summarizePrompt = "Summarize the following conversation while preserving key points and context. Make it concise but include important details:\n\n";
            foreach (var message in _chatHistory)
            {
                summarizePrompt += $"{message.Role}: {message.Content}\n";
            }

            try
            {
                _summarizedContext = await _llmClient.GenerateTextResponseAsync(summarizePrompt, modelToUse);
                _chatHistory.Clear();
                System.Diagnostics.Debug.WriteLine("Context summarization successful");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during summarization: {ex.Message}");
                throw new InvalidOperationException($"Failed to summarize context with model '{modelToUse}'", ex);
            }
        }

        /// <summary>
        /// Clears the current context
        /// </summary>
        public void ClearContext()
        {
            _chatHistory.Clear();
            _summarizedContext = null;
        }

        /// <summary>
        /// Gets diagnostic information about the context manager
        /// </summary>
        public string GetDebugInfo()
        {
            StringBuilder debug = new StringBuilder();
            debug.AppendLine("Context Manager Status:");
            debug.AppendLine($"Context Enabled: {IsContextEnabled}");
            debug.AppendLine($"Messages in History: {_chatHistory.Count}");
            debug.AppendLine($"Has Summarized Context: {!string.IsNullOrEmpty(_summarizedContext)}");
            debug.AppendLine($"Estimated Tokens: {GetCurrentContextTokens()}");
            debug.AppendLine($"Default Model: {DefaultModel ?? "(not set)"}");
            return debug.ToString();
        }

        /// <summary>
        /// Gets the full current context
        /// </summary>
        public string GetFullContext()
        {
            StringBuilder context = new StringBuilder();
            context.AppendLine("=== FULL CURRENT CONTEXT ===\n");

            if (!string.IsNullOrEmpty(_summarizedContext))
            {
                context.AppendLine("=== SUMMARIZED HISTORY ===");
                context.AppendLine(_summarizedContext);
                context.AppendLine("\n=== RECENT MESSAGES ===");
            }

            foreach (var message in _chatHistory)
            {
                context.AppendLine($"[{message.Timestamp:HH:mm:ss}] {message.Role}: {message.Content}");
            }

            if (_chatHistory.Count == 0 && string.IsNullOrEmpty(_summarizedContext))
            {
                context.AppendLine("(Context is empty)");
            }

            return context.ToString();
        }
        
        /// <summary>
        /// Refreshes the context (used when switching between chat sessions)
        /// </summary>
        public virtual void RefreshContext()
        {
            // Base implementation doesn't need special handling
            // This method is overridden in derived classes
        }
    }
}