using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;

namespace AIAgentTest.API_Clients
{
    public class OllamaClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _ollamaBaseUrl;

        public OllamaClient(string ollamaBaseUrl = "http://localhost:11434")
        {
            _httpClient = new HttpClient();
            _ollamaBaseUrl = ollamaBaseUrl;
        }

        public async Task<string> GenerateTextResponseAsync(string prompt, string model = "llama3")
        {
            var responseJson = await GenerateResponseAsync(prompt, model);
            var responseText = ExtractPlainTextResponse(responseJson);

            return responseText;
        }

        public string ExtractPlainTextResponse(string jsonLines)
        {
            var responseBuilder = new StringBuilder();

            foreach (var line in jsonLines.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(line))
                    {
                        JsonElement root = doc.RootElement;
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

            return responseBuilder.ToString();
        }

        public async Task<string> GenerateResponseWithImageAsync(string prompt, string imagePath, string model = "x/llama3.2-vision:11b")
        {
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

        public async Task<string> GenerateResponseAsync(string prompt, string model = "llama3")
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
            // Parse the response and extract the generated text
            // This is a simplified example and may need adjustment based on Ollama's exact response format
            return responseContent;
        }

        public async Task<List<ModelInfo>> GetAvailableModelsAsync()
        {
            var response = await _httpClient.GetAsync($"{_ollamaBaseUrl}/api/tags");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var modelList = JsonSerializer.Deserialize<ModelList>(content, options);
            return modelList.Models;
        }

        public async Task<List<string>> GetLoadedModelsAsync()
        {
            var availableModels = await GetAvailableModelsAsync();
            var loadedModels = new List<string>();

            foreach (var model in availableModels)
            {
                var request = new { name = model.Name };
                var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_ollamaBaseUrl}/api/show", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    loadedModels.Add(model.Name);
                }
            }

            return loadedModels;
        }

        public async Task<List<string>> GetRunningModelsAsync()
        {
            var runningModels = new List<string>();
            var availableModels = await GetAvailableModelsAsync();

            foreach (var model in availableModels)
            {
                try
                {
                    // We'll use a very short prompt to check if the model responds quickly
                    var response = await GenerateResponseAsync("are you running?", model.Name);
                    runningModels.Add(model.Name);
                }
                catch
                {
                    // If an exception occurs, the model is likely not running
                }
            }

            return runningModels;
        }

        public async Task LoadModelAsync(string modelName)
        {
            var request = new { name = modelName };
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_ollamaBaseUrl}/api/pull", content);
            response.EnsureSuccessStatusCode();
        }

        public async Task<string> ProcessQueryWithFunctions(string query, string model = "reader-lm:0.5b-fp16")
        {
            // First, use the model to interpret the query
            var interpretation = await GenerateResponseAsync($"Interpret this query and decide if it requires a web search: {query}", model);

            if (interpretation.Contains("web search", StringComparison.OrdinalIgnoreCase))
            {
                // If web search is needed, use the model to generate a search query
                var searchQuery = await GenerateResponseAsync($"Generate a web search query for: {query}", model);

                // Perform the web search
                var searchResults = await PerformWebSearch(searchQuery);

                // Use the model to process and summarize the search results
                var summary = await GenerateResponseAsync($"Summarize these search results to answer the original query: {query}\n\nSearch results: {searchResults}", model);

                return summary;
            }
            else
            {
                // If no web search is needed, just use the model to answer directly
                return await GenerateResponseAsync(query, model);
            }
        }

        public async Task<string> PerformWebSearch2(string query)
        {
            // This is a simple web scraping example using Google search results
            var url = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}";
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(content);

            // Extract search result snippets
            var snippets = doc.DocumentNode.SelectNodes("//div[@class='g']//div[@class='s']//div[@class='st']");

            if (snippets != null && snippets.Count > 0)
            {
                return string.Join("\n", snippets.Take(3).Select(s => s.InnerText.Trim()));
            }

            return "No results found.";
        }

        public async Task<string> PerformWebSearch(string query)
        {
            // This is a placeholder. In a real implementation, you would use a proper search API.
            var queryContent = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(query)}&format=json";
            var response = await _httpClient.GetAsync(queryContent);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            // Parse the JSON response and extract relevant information
            // This is a simplified example and would need to be adapted based on the actual API response
            using (JsonDocument doc = JsonDocument.Parse(content))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("Abstract", out JsonElement abstractText))
                {
                    return abstractText.GetString();
                }
            }

            return "No results found.";
        }
    }

    public class VRAMUsage
    {
        public long TotalVRAM { get; set; }
        public long UsedVRAM { get; set; }
        public long AvailableVRAM { get; set; }
    }

    public class ModelList
    {
        public List<ModelInfo> Models { get; set; }
    }

    public class ModelInfo
    {
        public string Name { get; set; }
        public string Model { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long Size { get; set; }
        //public int SizeInGb { get { return (int)(Size/1024/1024/1024); } }
        public int SizeInGb { get { return (int)(Size/1000000000); } }
        public string Digest { get; set; }
        public ModelDetails Details { get; set; }
    }

    public class ModelDetails
    {
        public string ParentModel { get; set; }
        public string Format { get; set; }
        public string Family { get; set; }
        public List<string> Families { get; set; }
        public string ParameterSize { get; set; }
        public string QuantizationLevel { get; set; }
    }

    public class TagList
    {
        public List<string> Models { get; set; }
    }

    public class AgentAction
    {
        public string Description { get; set; }
        public Func<string, Task<string>> ExecuteAsync { get; set; }
    }

    public class AgentOrchestrator
    {
        private readonly OllamaClient _agent;
        private readonly List<AgentAction> _actions = new List<AgentAction>();

        public AgentOrchestrator(OllamaClient agent)
        {
            _agent = agent;
        }

        public void AddAction(string description, Func<string, Task<string>> executeAsync)
        {
            _actions.Add(new AgentAction { Description = description, ExecuteAsync = executeAsync });
        }

        public async Task RunActionsAsync()
        {
            foreach (var action in _actions)
            {
                Console.WriteLine($"Executing action: {action.Description}");
                try
                {
                    var result = await action.ExecuteAsync(action.Description);
                    Console.WriteLine($"Action result: {result}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error executing action: {ex.Message}");
                }
            }
        }


    }
}
