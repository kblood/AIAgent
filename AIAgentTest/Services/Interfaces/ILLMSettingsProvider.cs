namespace AIAgentTest.Services.Interfaces
{
    /// <summary>
    /// Interface for LLM services that support runtime settings changes
    /// </summary>
    public interface ILLMSettingsProvider
    {
        /// <summary>
        /// Set the temperature for generation
        /// </summary>
        /// <param name="temperature">Temperature value (typically 0.0-1.0)</param>
        void SetTemperature(double temperature);
        
        /// <summary>
        /// Set the maximum number of tokens to generate
        /// </summary>
        /// <param name="maxTokens">Maximum tokens</param>
        void SetMaxTokens(int maxTokens);
        
        /// <summary>
        /// Set the top-p sampling value
        /// </summary>
        /// <param name="topP">Top-p value (0.0-1.0)</param>
        void SetTopP(double topP);
        
        /// <summary>
        /// Set the frequency penalty
        /// </summary>
        /// <param name="frequencyPenalty">Frequency penalty value</param>
        void SetFrequencyPenalty(double frequencyPenalty);
        
        /// <summary>
        /// Set the presence penalty
        /// </summary>
        /// <param name="presencePenalty">Presence penalty value</param>
        void SetPresencePenalty(double presencePenalty);
    }
}