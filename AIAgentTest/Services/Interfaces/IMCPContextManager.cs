using System.Threading.Tasks;

namespace AIAgentTest.Services.Interfaces
{
    /// <summary>
    /// Interface for MCP-aware context manager
    /// </summary>
    public interface IMCPContextManager : IContextManager
    {
        /// <summary>
        /// Get MCP contextual prompt
        /// </summary>
        /// <param name="input">User input</param>
        /// <returns>Prompt with MCP context</returns>
        string GetMCPContextualPrompt(string input);
        
        /// <summary>
        /// Add tool use to context
        /// </summary>
        /// <param name="toolName">Tool name</param>
        /// <param name="toolInput">Tool input</param>
        void AddToolUse(string toolName, object toolInput);
        
        /// <summary>
        /// Add tool result to context
        /// </summary>
        /// <param name="toolName">Tool name</param>
        /// <param name="toolResult">Tool result</param>
        /// <param name="success">Whether the tool succeeded</param>
        /// <param name="errorMessage">Error message if the tool failed</param>
        void AddToolResult(string toolName, object toolResult, bool success, string errorMessage = null);
        
        /// <summary>
        /// Get information about tool usage in the current context
        /// </summary>
        /// <returns>Tool usage information as a formatted string</returns>
        string GetToolUsageInfo();
        
        /// <summary>
        /// Get token statistics for the current context
        /// </summary>
        /// <returns>Token statistics as a formatted string</returns>
        string GetTokenStatistics();
    }
}
