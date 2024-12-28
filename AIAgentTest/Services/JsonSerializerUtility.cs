using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public static class JsonSerializerUtility
{
    public static async Task SaveToFileAsync<T>(T obj, string filePath)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(obj, options);
        await File.WriteAllTextAsync(filePath, json);
    }

    public static async Task<T> LoadFromFileAsync<T>(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<T>(json);
    }

    public static string GenerateFileName(string prefix = "data")
    {
        return $"{prefix}_{DateTime.Now:yyyyMMddHHmmss}.json";
    }
}