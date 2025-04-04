using AIAgentTest.API_Clients;
using AIAgentTest.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AIAgentTest.Services
{
    public class LLMClientService : ILLMClientService, ILLMSettingsProvider
    {
        private readonly ILLMClient _llmClient;
        
        // Default settings
        private double _temperature = 0.7;
        private int _maxTokens = 2048;
        private double _topP = 0.9;
        private double _frequencyPenalty = 0.0;
        private double _presencePenalty = 0.0;
        
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
        
        #region ILLMSettingsProvider Implementation
        
        public void SetTemperature(double temperature)
        {
            _temperature = temperature;
            
            // Apply to client if it supports settings
            if (_llmClient is ILLMSettingsClient settingsClient)
            {
                settingsClient.SetTemperature(temperature);
            }
        }
        
        public void SetMaxTokens(int maxTokens)
        {
            _maxTokens = maxTokens;
            
            // Apply to client if it supports settings
            if (_llmClient is ILLMSettingsClient settingsClient)
            {
                settingsClient.SetMaxTokens(maxTokens);
            }
        }
        
        public void SetTopP(double topP)
        {
            _topP = topP;
            
            // Apply to client if it supports settings
            if (_llmClient is ILLMSettingsClient settingsClient)
            {
                settingsClient.SetTopP(topP);
            }
        }
        
        public void SetFrequencyPenalty(double frequencyPenalty)
        {
            _frequencyPenalty = frequencyPenalty;
            
            // Apply to client if it supports settings
            if (_llmClient is ILLMSettingsClient settingsClient)
            {
                settingsClient.SetFrequencyPenalty(frequencyPenalty);
            }
        }
        
        public void SetPresencePenalty(double presencePenalty)
        {
            _presencePenalty = presencePenalty;
            
            // Apply to client if it supports settings
            if (_llmClient is ILLMSettingsClient settingsClient)
            {
                settingsClient.SetPresencePenalty(presencePenalty);
            }
        }
        
        #endregion
    }
}