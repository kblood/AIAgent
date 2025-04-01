using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIAgentTest.API_Clients
{
    /// <summary>
    /// Client for interacting with Ollama's API
    /// </summary>
    public class OllamaClient : BaseLLMClient
    {
        private readonly string _ollamaBaseUrl;

        /// <summary>
        /// Initializes a new instance of the OllamaClient
        /// </summary>
        /// <param name="ollamaBaseUrl">The base URL for the Ollama API (default: http://localhost:11434)</param>
        public OllamaClient(string ollamaBaseUrl = "http://localhost:11434") 
            : base("Ollama")
        {
            _ollamaBaseUrl = ollamaBaseUrl;
        }

        /// <summary>
        /// Generates a text response from the specified model
        /// </summary>
        public override async Task<string> GenerateTextResponseAsync(string prompt, string model = null)
        {
            model ??= "llama3"; // Default model if none specified
            
            var responseJson = await GenerateResponseAsync(prompt, model);
            var responseText = ExtractPlainTextResponse(responseJson);

            return responseText;
        }

        /// <summary>
        /// Generates a text response as a stream of tokens
        /// </summary>
        public override async IAsyncEnumerable<string> GenerateStreamResponseAsync(string prompt, string model = null)
        {
            model ??= "llama3"; // Default model if none specified
            
            var request = new
            {
                model = model,
                prompt = prompt,
                stream = true
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync($"{_ollamaBaseUrl}/api/generate", content);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line)) continue;

                string? result = null;
                try
                {
                    using var jsonDoc = JsonDocument.Parse(line);
                    if (jsonDoc.RootElement.TryGetProperty("response", out var responseElement))
                    {
                        result = responseElement.GetString();
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed JSON
                    continue;
                }

                if (!string.IsNullOrEmpty(result))
                {
                    yield return result;
                }
            }
        }

        /// <summary>
        /// Processes an image with a vision-capable model
        /// </summary>
        public override async Task<string> GenerateResponseWithImageAsync(string prompt, string imagePath, string model = null)
        {
            model ??= "x/llama3.2-vision:11b"; // Default vision model
            
            byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);
            string base64Image = Convert.ToBase64String(imageBytes);

            var request = new
            {
                model = model,
                prompt = prompt,
                images = new[] { base64Image }
            };

            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_ollamaBaseUrl}/api/generate", content);

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            var textResponse = ExtractPlainTextResponse(responseContent);
            return textResponse;
        }

        /// <summary>
        /// Gets a list of available models from Ollama
        /// </summary>
        public override async Task<List<string>> GetAvailableModelsAsync()
        {
            var response = await _httpClient.GetAsync($"{_ollamaBaseUrl}/api/tags");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            
            var modelList = JsonSerializer.Deserialize<OllamaModelList>(content, _jsonOptions);
            return modelList?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
        }

        /// <summary>
        /// Gets detailed information about a specific model
        /// </summary>
        public override async Task<ModelInfo> GetModelInfoAsync(string modelName)
        {
            var response = await _httpClient.GetAsync($"{_ollamaBaseUrl}/api/tags");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            
            var modelList = JsonSerializer.Deserialize<OllamaModelList>(content, _jsonOptions);
            var ollamaModel = modelList?.Models?.FirstOrDefault(m => m.Name == modelName);
            
            if (ollamaModel == null)
            {
                throw new ArgumentException($"Model '{modelName}' not found in available models");
            }
            
            var modelInfo = new ModelInfo
            {
                Name = ollamaModel.Name,
                Provider = "Ollama",
                Size = ollamaModel.Size,
                Family = ollamaModel.Details?.Family
            };
            
            // Add capabilities based on model family/type
            if (ollamaModel.Name.Contains("vision"))
            {
                modelInfo.Capabilities["vision"] = true;
            }
            
            // Add metadata
            if (ollamaModel.Details != null)
            {
                modelInfo.Metadata["format"] = ollamaModel.Details.Format;
                modelInfo.Metadata["parameter_size"] = ollamaModel.Details.ParameterSize;
                modelInfo.Metadata["quantization_level"] = ollamaModel.Details.QuantizationLevel;
            }
            
            return modelInfo;
        }

        /// <summary>
        /// Generates a response using the function calling capability
        /// </summary>
        public override async Task<string> GenerateWithFunctionsAsync(string prompt, string model, List<FunctionDefinition> functions)
        {
            var request = new
            {
                model = model,
                prompt = $"{prompt}\n\nAvailable functions:\n{JsonSerializer.Serialize(functions, _jsonOptions)}",
                system = "You are an AI assistant that can call functions. If you need to use a function and the function is available, respond with a JSON object containing 'name' and 'arguments'. Otherwise, respond normally.",
                options = new
                {
                    num_ctx = 2048 * 4,
                    low_vram = false,
                    use_mmap = true,
                    use_mlock = false,
                    use_kv_cache = true
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_ollamaBaseUrl}/api/generate", content);

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();

            return ExtractPlainTextResponse(responseContent);
        }

        /// <summary>
        /// Loads a model into Ollama
        /// </summary>
        public override async Task LoadModelAsync(string modelName)
        {
            var request = new { name = modelName };
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_ollamaBaseUrl}/api/pull", content);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>
        /// Gets a list of models that are currently loaded in memory
        /// </summary>
        public async Task<List<string>> GetLoadedModelsAsync()
        {
            var availableModels = await GetAvailableModelsAsync();
            var loadedModels = new List<string>();

            foreach (var model in availableModels)
            {
                var request = new { name = model };
                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_ollamaBaseUrl}/api/show", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    loadedModels.Add(model);
                }
            }

            return loadedModels;
        }

        /// <summary>
        /// Gets information about running models
        /// </summary>
        public async Task<List<string>> GetRunningModelsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_ollamaBaseUrl}/api/ps");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                
                var modelList = JsonSerializer.Deserialize<OllamaModelList>(content, _jsonOptions);
                return modelList?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting running models: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Private helper method to generate a response from Ollama
        /// </summary>
        private async Task<string> GenerateResponseAsync(string prompt, string model)
        {
            var request = new
            {
                model = model,
                prompt = prompt
            };

            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_ollamaBaseUrl}/api/generate", content);

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();
            
            return responseContent;
        }

        /// <summary>
        /// Gets response statistics from Ollama
        /// </summary>
        public Dictionary<string, string> ExtractResponseStats(string jsonLines)
        {
            try
            {
                Dictionary<string, string> responseStats = new Dictionary<string, string>();

                foreach (var line in jsonLines.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        
                        CollectStatIfExists(root, "done_reason", responseStats);
                        CollectStatIfExists(root, "total_duration", responseStats);
                        CollectStatIfExists(root, "prompt_eval_count", responseStats);
                        CollectStatIfExists(root, "prompt_eval_duration", responseStats);
                        CollectStatIfExists(root, "eval_count", responseStats);
                        CollectStatIfExists(root, "eval_duration", responseStats);
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Failed to parse JSON: {ex.Message}");
                    }
                }

                return responseStats;
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }
        
        private void CollectStatIfExists(JsonElement root, string propertyName, Dictionary<string, string> stats)
        {
            if (root.TryGetProperty(propertyName, out var element))
            {
                stats[propertyName] = element.ToString();
            }
        }
        
        #region Ollama Specific Classes
        
        // These classes are specific to Ollama's API response format
        private class OllamaModelList
        {
            public List<OllamaModelInfo> Models { get; set; }
        }

        private class OllamaModelInfo
        {
            public string Name { get; set; }
            public string Model { get; set; }
            public DateTime ModifiedAt { get; set; }
            public long Size { get; set; }
            public string Digest { get; set; }
            public OllamaModelDetails Details { get; set; }
        }

        private class OllamaModelDetails
        {
            public string ParentModel { get; set; }
            public string Format { get; set; }
            public string Family { get; set; }
            public List<string> Families { get; set; }
            public string ParameterSize { get; set; }
            public string QuantizationLevel { get; set; }
        }
        
        #endregion
    }
}