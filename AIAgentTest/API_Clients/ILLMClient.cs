using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIAgentTest.API_Clients
{
    /// <summary>
    /// Interface for all LLM client implementations
    /// </summary>
    public interface ILLMClient
    {
        /// <summary>
        /// Gets a list of available models from the provider
        /// </summary>
        /// <returns>A list of model identifiers</returns>
        Task<List<string>> GetAvailableModelsAsync();

        /// <summary>
        /// Gets a list of available models from the provider (alias for backward compatibility)
        /// </summary>
        /// <returns>A list of model identifiers</returns>
        Task<List<string>> GetModelsAsync() => GetAvailableModelsAsync();

        /// <summary>
        /// Generates a text response from the LLM
        /// </summary>
        /// <param name="prompt">The prompt to send to the LLM</param>
        /// <param name="model">The model identifier to use (optional)</param>
        /// <returns>The generated text response</returns>
        Task<string> GenerateTextResponseAsync(string prompt, string model = null);
        
        /// <summary>
        /// Generates a text response from the LLM (alias for backward compatibility)
        /// </summary>
        /// <param name="prompt">The prompt to send to the LLM</param>
        /// <param name="model">The model identifier to use (optional)</param>
        /// <returns>The generated text response</returns>
        Task<string> GenerateTextAsync(string prompt, string model = null) => GenerateTextResponseAsync(prompt, model);
        
        /// <summary>
        /// Generates a text response from the LLM with streaming capability
        /// </summary>
        /// <param name="prompt">The prompt to send to the LLM</param>
        /// <param name="model">The model identifier to use (optional)</param>
        /// <returns>An async enumerable of response chunks</returns>
        IAsyncEnumerable<string> GenerateStreamResponseAsync(string prompt, string model = null);
        
        /// <summary>
        /// Generates a text response from the LLM with streaming capability (alias for backward compatibility)
        /// </summary>
        /// <param name="prompt">The prompt to send to the LLM</param>
        /// <param name="model">The model identifier to use (optional)</param>
        /// <returns>An async enumerable of response chunks</returns>
        IAsyncEnumerable<string> GenerateStreamTextAsync(string prompt, string model = null) => GenerateStreamResponseAsync(prompt, model);
        
        /// <summary>
        /// Processes an image and generates a text response 
        /// </summary>
        /// <param name="prompt">The text prompt to accompany the image</param>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="model">The model identifier to use (optional)</param>
        /// <returns>The generated text response</returns>
        Task<string> GenerateResponseWithImageAsync(string prompt, string imagePath, string model = null);
        
        /// <summary>
        /// Loads a model into memory for faster inference
        /// </summary>
        /// <param name="modelName">The name of the model to load</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task LoadModelAsync(string modelName);
        
        /// <summary>
        /// Gets model-specific information and capabilities
        /// </summary>
        /// <param name="modelName">The name of the model to query</param>
        /// <returns>Model information</returns>
        Task<ModelInfo> GetModelInfoAsync(string modelName);
        
        /// <summary>
        /// Generates a response with function calling capabilities
        /// </summary>
        /// <param name="prompt">The prompt to send to the LLM</param>
        /// <param name="model">The model identifier to use</param>
        /// <param name="functions">List of function definitions available to the model</param>
        /// <returns>The generated response that may include function calls</returns>
        Task<string> GenerateWithFunctionsAsync(string prompt, string model, List<FunctionDefinition> functions);
    }
    
    /// <summary>
    /// Common model information class that all clients can use
    /// </summary>
    public class ModelInfo
    {
        public string Name { get; set; }
        public string Provider { get; set; }
        public long Size { get; set; }
        public string Family { get; set; }
        public Dictionary<string, object> Capabilities { get; set; } = new Dictionary<string, object>();
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
    
    /// <summary>
    /// Common function definition structure for function calling
    /// </summary>
    public class FunctionDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, ParameterDefinition> Parameters { get; set; } = new Dictionary<string, ParameterDefinition>();
    }
    
    /// <summary>
    /// Parameter definition for function calling
    /// </summary>
    public class ParameterDefinition
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public object DefaultValue { get; set; }
        public Dictionary<string, object> Constraints { get; set; } = new Dictionary<string, object>();
    }
}