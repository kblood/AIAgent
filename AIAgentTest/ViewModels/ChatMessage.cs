using System;

namespace AIAgentTest.ViewModels
{
    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public string ToolName { get; set; }
        public string ToolContent { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
        
        public ChatMessage(string role, string content, string toolName, string toolContent) : this(role, content)
        {
            ToolName = toolName;
            ToolContent = toolContent;
        }
    }
}