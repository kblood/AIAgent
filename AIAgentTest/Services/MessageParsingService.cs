using AIAgentTest.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AIAgentTest.Services
{
    public class MessageParsingService : IMessageParsingService
    {
        public event EventHandler<CodeExtractedEventArgs> CodeExtracted;

        public void ProcessMessage(string message, Action<string> textCallback, Action<string, string> codeCallback)
        {
            string pattern = @"```(\w*)\r?\n(.*?)\r?\n```|```(\w*)\s*(.*?)```";
            int lastIndex = 0;

            var matches = Regex.Matches(message, pattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                string textBefore = message.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrWhiteSpace(textBefore))
                {
                    textCallback(textBefore);
                }

                string language = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
                string code = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;

                codeCallback(language, code.Trim());
                CodeExtracted?.Invoke(this, new CodeExtractedEventArgs(code.Trim(), language));

                lastIndex = match.Index + match.Length;
            }

            string remaining = message.Substring(lastIndex);
            if (!string.IsNullOrWhiteSpace(remaining))
            {
                textCallback(remaining);
            }
        }

        public IEnumerable<(string Text, string Language, string Code)> ExtractCodeBlocks(string message)
        {
            string pattern = @"```(\w*)\r?\n(.*?)\r?\n```|```(\w*)\s*(.*?)```";
            int lastIndex = 0;
            var results = new List<(string Text, string Language, string Code)>();

            var matches = Regex.Matches(message, pattern, RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                string textBefore = message.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrWhiteSpace(textBefore))
                {
                    results.Add((textBefore, null, null));
                }

                string language = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
                string code = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;

                results.Add((null, language, code.Trim()));
                lastIndex = match.Index + match.Length;
            }

            string remaining = message.Substring(lastIndex);
            if (!string.IsNullOrWhiteSpace(remaining))
            {
                results.Add((remaining, null, null));
            }

            return results;
        }
    }
}