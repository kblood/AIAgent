using AIAgentTest.Services.MCP;
using System;
using System.Collections.Generic;

namespace AIAgentTest.Services.Interfaces
{
    public class CodeExtractedEventArgs : EventArgs
    {
        public string Code { get; }
        public string Language { get; }
        
        public CodeExtractedEventArgs(string code, string language)
        {
            Code = code;
            Language = language;
        }
    }

    public interface IMessageParsingService
    {
        event EventHandler<CodeExtractedEventArgs> CodeExtracted;
        
        void ProcessMessage(string message, Action<string> textCallback, Action<string, string> codeCallback);
        
        IEnumerable<(string Text, string Language, string Code)> ExtractCodeBlocks(string message);
        
        /// <summary>
        /// Extracts an MCP response from a text message
        /// </summary>
        /// <param name="text">Text to parse</param>
        /// <returns>Parsed MCP response</returns>
        MCPResponse ExtractMCPResponse(string text);
        
        /// <summary>
        /// Extracts tool call information from a text message
        /// </summary>
        /// <param name="text">Text to parse</param>
        /// <returns>Tool name, parameters dictionary, and text before the tool call</returns>
        (string ToolName, Dictionary<string, object> Parameters, string TextBeforeCall) ExtractToolCall(string text);
    }
}