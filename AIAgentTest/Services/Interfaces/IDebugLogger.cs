using System;

namespace AIAgentTest.Services.Interfaces
{
    /// <summary>
    /// Interface for debug logging capabilities
    /// </summary>
    public interface IDebugLogger
    {
        /// <summary>
        /// Log a message to the debug panel
        /// </summary>
        /// <param name="message">Message to log</param>
        void Log(string message);
        
        /// <summary>
        /// Clear all log entries
        /// </summary>
        void Clear();
    }
}
