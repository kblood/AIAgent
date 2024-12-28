using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class LMStudioClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _lmStudioPath;

    public LMStudioClient(string baseUrl = "http://localhost:1234/v1", string lmStudioPath = null)
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