using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace AIAgentTest.API_Clients
{
    /// <summary>
    /// Implementation of ILLMClient for LM Studio API
    /// </summary>
    public class LMStudioClient : BaseLLMClient
    {
        private readonly string _lmStudioPath;
        private readonly string _baseUrl;
        private readonly LMStudioLocalClient _localClient;
        
        /// <summary>
        /// Initialize a new LM Studio client
        /// </summary>
        /// <param name="baseUrl">Base URL for the LM Studio API</param>
        /// <param name="lmStudioPath">Path to LM Studio installation (optional)</param>
        public LMStudioClient(string baseUrl = "http://localhost:1234/v1", string lmStudioPath = null)
            : base("LM Studio")
        {
            _baseUrl = baseUrl;
            _lmStudioPath = lmStudioPath ?? GetDefaultLMStudioPath();
            _localClient = new LMStudioLocalClient(_baseUrl, _lmStudioPath);
        }
        
        private string GetDefaultLMStudioPath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "LM Studio");
        }
        
        /// <summary>
        /// Get available models from LM Studio
        /// </summary>
        public override async Task<List<string>> GetAvailableModelsAsync()
        {
            try
            {
                // Try to get loaded models from the API first
                var loadedModels = await _localClient.ListLoadedModelsAsync();
                if (loadedModels.Count > 0)
                {
                    return loadedModels;
                }
                
                // Fall back to installed models if no loaded models
                return _localClient.ListInstalledModels();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting models from LM Studio: {ex.Message}");
                return new List<string>();
            }
        }
        
        /// <summary>
        /// Generate a text response using LM Studio
        /// </summary>
        public override async Task<string> GenerateTextResponseAsync(string prompt, string model = null)
        {
            return await _localClient.GenerateResponseAsync(prompt, model);
        }
        
        /// <summary>
        /// Generate a streaming response (not fully implemented)
        /// </summary>
        public override async IAsyncEnumerable<string> GenerateStreamResponseAsync(string prompt, string model = null)
        {
            // Simple implementation that returns the full response as a single chunk
            string response = await _localClient.GenerateResponseAsync(prompt, model);
            yield return response;
        }
        
        /// <summary>
        /// Generate a response with function calls (not supported in LM Studio)
        /// </summary>
        public override Task<string> GenerateWithFunctionsAsync(string prompt, string model, List<FunctionDefinition> functions)
        {
            // LM Studio doesn't directly support function calling
            // We can approximate it by adding function definitions to the prompt
            var enhancedPrompt = $"{prompt}\n\nAvailable functions:\n{JsonSerializer.Serialize(functions, new JsonSerializerOptions { WriteIndented = true })}\n\nIf you need to call a function, respond with a JSON object with 'name' and 'arguments'";
            
            return GenerateTextResponseAsync(enhancedPrompt, model);
        }
        
        /// <summary>
        /// Get model info
        /// </summary>
        public override async Task<ModelInfo> GetModelInfoAsync(string modelName)
        {
            var models = await GetAvailableModelsAsync();
            if (!models.Contains(modelName))
            {
                throw new ArgumentException($"Model '{modelName}' not found in available models");
            }
            
            return new ModelInfo
            {
                Name = modelName,
                Provider = "LM Studio",
                Family = "Local LLM"
            };
        }
    }
    
    /// <summary>
    /// Internal client for LM Studio interactions
    /// </summary>
    public class LMStudioLocalClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _lmStudioPath;

        public LMStudioLocalClient(string baseUrl = "http://localhost:1234/v1", string lmStudioPath = null)
        {
            _httpClient = new HttpClient();
            _baseUrl = baseUrl;
            _lmStudioPath = lmStudioPath ?? GetDefaultLMStudioPath();
        }

        private string GetDefaultLMStudioPath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "LM Studio");
        }

        public List<string> ListInstalledModels()
        {
            var modelsPath = Path.Combine(_lmStudioPath, "models");
            var installedModels = new List<string>();

            if (Directory.Exists(modelsPath))
            {
                foreach (var directory in Directory.GetDirectories(modelsPath))
                {
                    installedModels.Add(Path.GetFileName(directory));
                }
            }

            return installedModels;
        }

        public async Task<List<string>> ListLoadedModelsAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/models");
            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();

            var models = new List<string>();
            using (JsonDocument doc = JsonDocument.Parse(responseBody))
            {
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("data", out JsonElement data) && data.ValueKind == JsonValueKind.Array)
                {
                    foreach (var model in data.EnumerateArray())
                    {
                        if (model.TryGetProperty("id", out JsonElement id))
                        {
                            models.Add(id.GetString());
                        }
                    }
                }
            }

            return models;
        }

        public async Task<string> GenerateResponseAsync(string prompt, string modelName = null)
        {
            var request = new
            {
                model = modelName,
                prompt = prompt,
                max_tokens = 100,
                temperature = 0.7
            };

            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_baseUrl}/completions", content);

            response.EnsureSuccessStatusCode();
            var responseBody = await response.Content.ReadAsStringAsync();

            using (JsonDocument doc = JsonDocument.Parse(responseBody))
            {
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0)
                {
                    return choices[0].GetProperty("text").GetString();
                }
            }

            return string.Empty;
        }
    }
}