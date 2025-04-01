using AIAgentTest.API_Clients;
using AIAgentTest.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AIAgentTest.Services
{
    public class LLMClientService : ILLMClientService
    {
        private readonly ILLMClient _llmClient;
        
        public event EventHandler<ModelLoadedEventArgs> ModelsLoaded;

        public LLMClientService(ILLMClient llmClient)
        {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        }

        public async Task<IEnumerable<string>> GetAvailableModelsAsync()
        {
            try
            {
                var models = await _llmClient.GetAvailableModelsAsync();
                
                ModelsLoaded?.Invoke(this, new ModelLoadedEventArgs(models));
                return models;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting models: {ex.Message}");
                return Enumerable.Empty<string>();
            }
        }

        public async Task<string> GenerateTextResponseAsync(string prompt, string model)
        {
            return await _llmClient.GenerateTextResponseAsync(prompt, model);
        }

        public async Task<string> GenerateResponseWithImageAsync(string prompt, string imagePath, string model)
        {
            return await _llmClient.GenerateResponseWithImageAsync(prompt, imagePath, model);
        }

        public IAsyncEnumerable<string> GenerateStreamResponseAsync(string prompt, string model)
        {
            return _llmClient.GenerateStreamResponseAsync(prompt, model);
        }
    }
}