using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIAgentTest.Services.Interfaces
{
    public class ModelLoadedEventArgs : EventArgs
    {
        public IEnumerable<string> Models { get; }

        public ModelLoadedEventArgs(IEnumerable<string> models)
        {
            Models = models;
        }
    }

    public interface ILLMClientService
    {
        Task<IEnumerable<string>> GetAvailableModelsAsync();
        Task<string> GenerateTextResponseAsync(string prompt, string model);
        Task<string> GenerateResponseWithImageAsync(string prompt, string imagePath, string model);
        IAsyncEnumerable<string> GenerateStreamResponseAsync(string prompt, string model);
        
        // Events
        event EventHandler<ModelLoadedEventArgs> ModelsLoaded;
    }
}