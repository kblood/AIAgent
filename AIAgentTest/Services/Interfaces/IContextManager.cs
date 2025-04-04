using System;
using System.Threading.Tasks;

namespace AIAgentTest.Services.Interfaces
{
    public interface IContextManager
    {
        bool IsContextEnabled { get; set; }
        string DefaultModel { get; set; }
        
        void AddMessage(string role, string content);
        void ClearContext();
        string GetContextualPrompt(string input);
        string GetFullContext();
        string GetDebugInfo();
        DateTime GetLastMessageTimestamp();
        Task SummarizeContext(string model);
        void RefreshContext();
    }
}