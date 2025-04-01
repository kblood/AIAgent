using AIAgentTest.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIAgentTest.Services.Interfaces
{
    public interface IChatSessionService
    {
        Task<IEnumerable<ChatSession>> LoadSessionsAsync();
        Task SaveSessionAsync(ChatSession session);
        Task DeleteSessionAsync(ChatSession session);
        Task<ChatSession> CreateSessionAsync(string name, string modelName);
    }
}