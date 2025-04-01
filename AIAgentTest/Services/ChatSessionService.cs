using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AIAgentTest.Models;
using AIAgentTest.Services.Interfaces;

namespace AIAgentTest.Services
{
    public class ChatSessionService : IChatSessionService
    {
        private readonly string _sessionsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Sessions");

        public ChatSessionService()
        {
            Directory.CreateDirectory(_sessionsDirectory);
        }

        public async Task SaveSessionAsync(ChatSession session)
        {
            session.UpdatedAt = DateTime.UtcNow;
            var filePath = Path.Combine(_sessionsDirectory, $"{session.Id}.json");
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(session));
        }

        public async Task<IEnumerable<ChatSession>> LoadSessionsAsync()
        {
            var sessions = new List<ChatSession>();
            foreach (var file in Directory.GetFiles(_sessionsDirectory, "*.json"))
            {
                var json = await File.ReadAllTextAsync(file);
                var session = JsonSerializer.Deserialize<ChatSession>(json);
                if (session != null)
                {
                    sessions.Add(session);
                }
            }
            return sessions.OrderByDescending(s => s.UpdatedAt).ToList();
        }

        public async Task DeleteSessionAsync(ChatSession session)
        {
            var filePath = Path.Combine(_sessionsDirectory, $"{session.Id}.json");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        public async Task<ChatSession> CreateSessionAsync(string name, string modelName)
        {
            var session = new ChatSession
            {
                Name = name,
                ModelName = modelName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            
            await SaveSessionAsync(session);
            return session;
        }
    }
}
