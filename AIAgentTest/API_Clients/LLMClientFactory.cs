using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIAgentTest.API_Clients
{
    /// <summary>
    /// Factory for creating and managing LLM client instances
    /// </summary>
    public class LLMClientFactory
    {
        private static readonly Dictionary<string, ILLMClient> _clients = new Dictionary<string, ILLMClient>();
        
        /// <summary>
        /// Available provider types
        /// </summary>
        public enum ProviderType
        {
            Ollama,
            LlamaSharp,
            LMStudio,
            OpenedAI
        }
        
        /// <summary>
        /// Get or create a client for the specified provider
        /// </summary>
        /// <param name="provider">The provider type</param>
        /// <param name="endpointUrl">Optional endpoint URL for API-based providers</param>
        /// <param name="modelPath">Optional model path for local model providers</param>
        /// <returns>An ILLMClient instance</returns>
        public static ILLMClient GetClient(ProviderType provider, string endpointUrl = null, string modelPath = null)
        {
            string key = provider.ToString() + (endpointUrl ?? "") + (modelPath ?? "");
            
            if (_clients.TryGetValue(key, out var existingClient))
            {
                return existingClient;
            }
            
            ILLMClient newClient = provider switch
            {
                ProviderType.Ollama => new OllamaClient(endpointUrl ?? "http://localhost:11434"),
                ProviderType.LlamaSharp => new LlamaSharpClient(modelPath),
                ProviderType.LMStudio => new LMStudioClient(endpointUrl),
                ProviderType.OpenedAI => new OpenedAIClient(endpointUrl),
                _ => throw new ArgumentException($"Unsupported provider type: {provider}")
            };
            
            _clients[key] = newClient;
            return newClient;
        }
        
        /// <summary>
        /// Release all clients and their resources
        /// </summary>
        public static void ReleaseAllClients()
        {
            foreach (var client in _clients.Values)
            {
                if (client is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            
            _clients.Clear();
        }
    }
    
    /// <summary>
    /// Extension methods to simplify working with clients from the factory
    /// </summary>
    public static class LLMClientFactoryExtensions
    {
        /// <summary>
        /// Gets the Ollama client with the default endpoint
        /// </summary>
        public static OllamaClient GetOllamaClient(this LLMClientFactory factory, string endpointUrl = null)
        {
            return (OllamaClient)LLMClientFactory.GetClient(
                LLMClientFactory.ProviderType.Ollama, 
                endpointUrl);
        }
        
        /// <summary>
        /// Gets the LlamaSharp client with the specified model path
        /// </summary>
        public static LlamaSharpClient GetLlamaSharpClient(this LLMClientFactory factory, string modelPath)
        {
            return (LlamaSharpClient)LLMClientFactory.GetClient(
                LLMClientFactory.ProviderType.LlamaSharp, 
                modelPath: modelPath);
        }
        
        /// <summary>
        /// Gets the LMStudio client with the default endpoint
        /// </summary>
        public static LMStudioClient GetLMStudioClient(this LLMClientFactory factory, string endpointUrl = null)
        {
            return (LMStudioClient)LLMClientFactory.GetClient(
                LLMClientFactory.ProviderType.LMStudio, 
                endpointUrl);
        }
    }
}