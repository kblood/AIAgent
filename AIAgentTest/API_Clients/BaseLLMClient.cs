using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;

namespace AIAgentTest.API_Clients
{
    /// <summary>
    /// Base implementation for LLM clients with shared functionality
    /// </summary>
    public abstract class BaseLLMClient : ILLMClient, IDisposable
    {
        protected readonly HttpClient _httpClient;
        protected readonly string _providerName;
        protected readonly JsonSerializerOptions _jsonOptions;
        
        protected BaseLLMClient(string providerName)
        {
            _providerName = providerName;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(15);
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };
        }
        
        /// <summary>
        /// Default implementation that should be overridden by providers that support image processing
        /// </summary>
        public virtual Task<string> GenerateResponseWithImageAsync(string prompt, string imagePath, string model = null)
        {
            throw new NotSupportedException($"The {_providerName} provider does not support image processing");
        }
        
        /// <summary>
        /// Default implementation that should be overridden by providers that support function calling
        /// </summary>
        public virtual Task<string> GenerateWithFunctionsAsync(string prompt, string model, List<FunctionDefinition> functions)
        {
            throw new NotSupportedException($"The {_providerName} provider does not support function calling");
        }
        
        /// <summary>
        /// Default implementation that should be overridden by providers that support model loading
        /// </summary>
        public virtual Task LoadModelAsync(string modelName)
        {
            throw new NotSupportedException($"The {_providerName} provider does not support explicit model loading");
        }
        
        /// <summary>
        /// Default implementation for model info - can be overridden for more specific implementations
        /// </summary>
        public virtual async Task<ModelInfo> GetModelInfoAsync(string modelName)
        {
            var models = await GetAvailableModelsAsync();
            if (!models.Contains(modelName))
            {
                throw new ArgumentException($"Model '{modelName}' not found in available models");
            }
            
            return new ModelInfo
            {
                Name = modelName,
                Provider = _providerName
            };
        }
        
        /// <summary>
        /// Extracts response from a JSON, preserving tool call structure if present
        /// </summary>
        protected string ExtractPlainTextResponse(string jsonLines)
        {
            var responseBuilder = new StringBuilder();
            string fullResponse = string.Empty;
            bool containsToolCall = false;

            // First check if the response contains a tool call
            if (jsonLines.Contains("\"type\":\"tool_use\"")
                || jsonLines.Contains("\"type\": \"tool_use\""))
            {
                containsToolCall = true;
                fullResponse = jsonLines;
            }

            // Process each line
            foreach (var line in jsonLines.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(line))
                    {
                        JsonElement root = doc.RootElement;
                        
                        // Check for tool call first
                        if (root.TryGetProperty("type", out JsonElement typeElement) &&
                            typeElement.GetString() == "tool_use" &&
                            root.TryGetProperty("tool", out _) &&
                            root.TryGetProperty("tool_input", out _))
                        {
                            // Return the full tool call JSON as is
                            return line;
                        }
                        
                        // Otherwise extract regular response text
                        if (root.TryGetProperty("response", out JsonElement responseElement))
                        {
                            responseBuilder.Append(responseElement.GetString());
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Failed to parse JSON: {ex.Message}");
                }
            }

            // If we found a tool call but couldn't parse it line by line,
            // return the full response for the OllamaMCPAdapter to process
            if (containsToolCall)
            {
                return fullResponse;
            }

            return responseBuilder.ToString();
        }
        
        /// <summary>
        /// Abstract methods that must be implemented by derived classes
        /// </summary>
        public abstract Task<List<string>> GetAvailableModelsAsync();
        public abstract Task<string> GenerateTextResponseAsync(string prompt, string model = null);
        public abstract IAsyncEnumerable<string> GenerateStreamResponseAsync(string prompt, string model = null);
        
        public void Dispose()
        {
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}