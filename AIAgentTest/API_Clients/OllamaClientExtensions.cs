using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIAgentTest.API_Clients
{
    /// <summary>
    /// Extensions for the OllamaClient to support additional parameters
    /// </summary>
    public static class OllamaClientExtensions
    {
        /// <summary>
        /// Generates a text response with custom parameters
        /// </summary>
        /// <param name="client">The OllamaClient instance</param>
        /// <param name="prompt">The prompt to send</param>
        /// <param name="model">The model to use</param>
        /// <param name="parameters">Additional parameters for the request</param>
        /// <returns>The generated text response</returns>
        public static async Task<string> GenerateTextResponseWithParamsAsync(
            this OllamaClient client, 
            string prompt, 
            string model = null,
            Dictionary<string, object> parameters = null)
        {
            model ??= "llama3"; // Default model if none specified

            // Create the base request
            var request = new Dictionary<string, object>
            {
                { "model", model },
                { "prompt", prompt }
            };

            // Add any additional parameters
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    // Don't override model or prompt
                    if (param.Key != "model" && param.Key != "prompt")
                    {
                        request[param.Key] = param.Value;
                    }
                }
            }

            // Create HTTP request
            var jsonContent = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Use reflection to access the private members of OllamaClient
            var httpClientField = typeof(BaseLLMClient).GetField("_httpClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var ollamaBaseUrlField = typeof(OllamaClient).GetField("_ollamaBaseUrl", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (httpClientField == null || ollamaBaseUrlField == null)
            {
                throw new InvalidOperationException("Could not access required fields in OllamaClient through reflection");
            }

            var httpClient = httpClientField.GetValue(client) as HttpClient;
            var ollamaBaseUrl = ollamaBaseUrlField.GetValue(client) as string;

            if (httpClient == null || ollamaBaseUrl == null)
            {
                throw new InvalidOperationException("Could not retrieve HttpClient or base URL from OllamaClient");
            }

            // Send the request
            var response = await httpClient.PostAsync($"{ollamaBaseUrl}/api/generate", content);
            response.EnsureSuccessStatusCode();
            var responseContent = await response.Content.ReadAsStringAsync();

            // Use reflection to access the private method that extracts the plain text
            var extractTextMethod = typeof(OllamaClient).GetMethod("ExtractPlainTextResponse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (extractTextMethod == null)
            {
                // Fallback extraction implementation
                using var jsonDoc = JsonDocument.Parse(responseContent);
                if (jsonDoc.RootElement.TryGetProperty("response", out var responseElement))
                {
                    return responseElement.GetString();
                }
                return responseContent;
            }
            
            return extractTextMethod.Invoke(client, new object[] { responseContent }) as string;
        }
    }
}
