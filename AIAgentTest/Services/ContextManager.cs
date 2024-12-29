using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using AIAgentTest.API_Clients;

namespace AIAgentTest.Services
{
    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }

        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
            Timestamp = DateTime.Now;
        }
    }

    public class ContextManager
    {
        private List<ChatMessage> _chatHistory;
        private string _summarizedContext;
        private readonly OllamaClient _ollamaClient;
        private int _maxTokensBeforeSummarize = 2000;
        private bool _isContextEnabled = true;

        public bool IsContextEnabled
        {
            get => _isContextEnabled;
            set => _isContextEnabled = value;
        }

        public ContextManager(OllamaClient ollamaClient)
        {
            _chatHistory = new List<ChatMessage>();
            _ollamaClient = ollamaClient;
        }

        public void AddMessage(string role, string content)
        {
            _chatHistory.Add(new ChatMessage(role, content));
        }

        public async Task<string> GetContextualPrompt(string userInput)
        {
            if (!IsContextEnabled)
                return userInput;

            StringBuilder contextBuilder = new StringBuilder();

            if (!string.IsNullOrEmpty(_summarizedContext))
            {
                contextBuilder.AppendLine("Previous conversation summary:");
                contextBuilder.AppendLine(_summarizedContext);
                contextBuilder.AppendLine();
            }

            // Add recent messages that haven't been summarized
            foreach (var message in _chatHistory)
            {
                contextBuilder.AppendLine($"{message.Role}: {message.Content}");
            }

            contextBuilder.AppendLine($"User: {userInput}");

            return contextBuilder.ToString();
        }

        public async Task<bool> ShouldSummarize()
        {
            return _chatHistory.Count > 0 && GetCurrentContextTokens() > _maxTokensBeforeSummarize;
        }

        private int GetCurrentContextTokens()
        {
            // Rough estimation: 1 token ≈ 4 characters
            int totalCharacters = _chatHistory.Sum(msg => msg.Content.Length);
            return totalCharacters / 4;
        }

        public async Task SummarizeContext(string modelName)
        {
            if (_chatHistory.Count == 0)
                return;

            string summarizePrompt = "Summarize the following conversation while preserving key points and context. Make it concise but include important details:\n\n";
            foreach (var message in _chatHistory)
            {
                summarizePrompt += $"{message.Role}: {message.Content}\n";
            }

            _summarizedContext = await _ollamaClient.GenerateTextResponseAsync(summarizePrompt, modelName);
            _chatHistory.Clear();
        }

        public void ClearContext()
        {
            _chatHistory.Clear();
            _summarizedContext = null;
        }

        public string GetDebugInfo()
        {
            StringBuilder debug = new StringBuilder();
            debug.AppendLine("Context Manager Status:");
            debug.AppendLine($"Context Enabled: {IsContextEnabled}");
            debug.AppendLine($"Messages in History: {_chatHistory.Count}");
            debug.AppendLine($"Has Summarized Context: {!string.IsNullOrEmpty(_summarizedContext)}");
            debug.AppendLine($"Estimated Tokens: {GetCurrentContextTokens()}");
            return debug.ToString();
        }

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
    }
}