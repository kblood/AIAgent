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
        void ProcessMessage(string message, Action<string> textCallback, Action<string, string> codeCallback);
        IEnumerable<(string Text, string Language, string Code)> ExtractCodeBlocks(string message);
        
        // Events
        event EventHandler<CodeExtractedEventArgs> CodeExtracted;
    }
}