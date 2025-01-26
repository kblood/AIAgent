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
    public class OpenedAI_VisionClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public OpenedAI_VisionClient(string baseUrl = "http://localhost:5006/v1", string apiKey = "sk-ip")
        {
            _httpClient = new HttpClient();
            _baseUrl = baseUrl;
            _apiKey = apiKey;
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<string> GenerateResponseWithImageAsync(string prompt, string imagePath, string model = "x/llama3.2-vision:11b")
        {
            byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);
            string base64Image = Convert.ToBase64String(imageBytes);
            // Determine the MIME type based on file extension
            string mimeType = GetContentTypeFromExtension(Path.GetExtension(imagePath));
            // Construct the data URI with the correct MIME type
            string imageDataUri = $"data:{mimeType};base64,{base64Image}";

            var request = new
            {
                model = model,
                messages = new[] 
                {
                    new 
                    {
                        role = "user",
                        content = new object[] 
                        {
                            new { type = "text", text = prompt },
                            new { type = "image_url", image_url = new { url = imageDataUri } }
                        }
                    }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            //var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content);
            var response = await _httpClient.PostAsync($"{_baseUrl}/chat/completions", content);

            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();

            // Similar to other code in your solution, parse the JSON for the assistant message
            using var jsonDoc = JsonDocument.Parse(responseContent);
            string responseText = jsonDoc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return responseText;
            //var textResponse = ExtractPlainTextResponse(responseContent);
            //return textResponse;
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

        private string GetContentTypeFromExtension(string extension)
        {
            return extension.ToLower() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                _ => "image/jpeg"
            };
        }
    }
}
