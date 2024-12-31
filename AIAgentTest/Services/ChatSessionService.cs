using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AIAgentTest.Models;

namespace AIAgentTest.Services
{
    public class ChatSessionService
    {
        private readonly string _sessionsDirectory;

        public ChatSessionService(string sessionsDirectory = null)
        {
            _sessionsDirectory = sessionsDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AIAgentTest",
                "ChatSessions"
            );
            Directory.CreateDirectory(_sessionsDirectory);
        }

        public async Task SaveSessionAsync(ChatSession session)
        {
            var filePath = Path.Combine(_sessionsDirectory, $"{session.Id}.json");
            var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task<ChatSession> LoadSessionAsync(string sessionId)
        {
            var filePath = Path.Combine(_sessionsDirectory, $"{sessionId}.json");
            if (!File.Exists(filePath))
                return null;

            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<ChatSession>(json);
        }

        public async Task<List<ChatSession>> ListSessionsAsync()
        {
            var sessions = new List<ChatSession>();
            foreach (var file in Directory.GetFiles(_sessionsDirectory, "*.json"))
            {
                var json = await File.ReadAllTextAsync(file);
                sessions.Add(JsonSerializer.Deserialize<ChatSession>(json));
            }
            return sessions;
        }

        public async Task DeleteSessionAsync(string sessionId)
        {
            var filePath = Path.Combine(_sessionsDirectory, $"{sessionId}.json");
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
