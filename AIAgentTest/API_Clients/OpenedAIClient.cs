using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIAgentTest.API_Clients
{
    /// <summary>
    /// Client implementation for the OpenedAI API
    /// </summary>
    public class OpenedAIClient : BaseLLMClient
    {
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly OpenedAI_VisionClient _visionClient;

        /// <summary>
        /// Initialize a new OpenedAIClient
        /// </summary>
        /// <param name="baseUrl">Base URL for the API</param>
        /// <param name="apiKey">API key for authentication</param>
        public OpenedAIClient(string baseUrl = "http://localhost:5006/v1", string apiKey = "sk-ip") 
            : base("OpenedAI")
        {
            _baseUrl = baseUrl;
            _apiKey = apiKey;
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _visionClient = new OpenedAI_VisionClient(baseUrl, apiKey);
        }

        /// <summary>
        /// Get available models
        /// </summary>
        public override async Task<List<string>> GetAvailableModelsAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/models");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            
            using var jsonDoc = JsonDocument.Parse(content);
            var models = new List<string>();
            
            var modelsArray = jsonDoc.RootElement.GetProperty("data");
            foreach (var model in modelsArray.EnumerateArray())
            {
                models.Add(model.GetProperty("id").GetString());
            }
            
            return models;
        }

        /// <summary>
        /// Generate a text response
        /// </summary>
        public override async Task<string> GenerateTextResponseAsync(string prompt, string model = null)
        {
            model ??= "text-davinci-003"; // Default model if none specified
            
            var request = new
            {
                model = model,
                messages = new[] 
                {
                    new { role = "user", content = prompt }
                }
            };
            
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content);
            
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            
            using var jsonDoc = JsonDocument.Parse(responseContent);
            string responseText = jsonDoc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return responseText;
        }

        /// <summary>
        /// Generate a text response with streaming
        /// </summary>
        public override async IAsyncEnumerable<string> GenerateStreamResponseAsync(string prompt, string model = null)
        {
            model ??= "text-davinci-003"; // Default model if none specified
            
            var request = new
            {
                model = model,
                messages = new[] 
                {
                    new { role = "user", content = prompt }
                },
                stream = true
            };
            
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content);
            
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line) || !line.StartsWith("data:")) continue;
                if (line == "data: [DONE]") break;
                
                line = line.Substring(5).Trim(); // Remove "data: " prefix
                
                string contentChunk = ExtractContentFromLine(line);
                if (!string.IsNullOrEmpty(contentChunk))
                {
                    yield return contentChunk;
                }
            }
        }
        
        private string ExtractContentFromLine(string line)
        {
            try
            {
                using var jsonDoc = JsonDocument.Parse(line);
                var choices = jsonDoc.RootElement.GetProperty("choices");
                
                if (choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("delta", out var delta) &&
                        delta.TryGetProperty("content", out var content_elem))
                    {
                        return content_elem.GetString();
                    }
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Failed to parse stream JSON: {ex.Message}");
            }
            
            return string.Empty;
        }

        /// <summary>
        /// Process an image using the vision client
        /// </summary>
        public override async Task<string> GenerateResponseWithImageAsync(string prompt, string imagePath, string model = null)
        {
            return await _visionClient.GenerateResponseWithImageAsync(prompt, imagePath, model);
        }
    }
}