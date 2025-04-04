using System.Threading.Tasks;

namespace AIAgentTest.API_Clients
{
    /// <summary>
    /// Interface for LLM clients with vision capabilities
    /// </summary>
    public interface IVisionCapableClient
    {
        /// <summary>
        /// Generates a text response from an image and prompt
        /// </summary>
        /// <param name="prompt">The prompt to send with the image</param>
        /// <param name="imagePath">Path to the image file</param>
        /// <param name="model">The model to use (optional)</param>
        /// <returns>The generated text response</returns>
        Task<string> GenerateTextWithImageAsync(string prompt, string imagePath, string model = null);
    }
}