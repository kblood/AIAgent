using System;
using System.Text.RegularExpressions;

namespace AIAgentTest.API_Clients.MCP
{
    /// <summary>
    /// Helper class to parse Markdown code blocks and extract tool calls
    /// </summary>
    public static class MarkdownParser
    {
        /// <summary>
        /// Extract JSON content from markdown code blocks
        /// </summary>
        /// <param name="text">Text containing possible markdown code blocks</param>
        /// <param name="preamble">Text before the code block (output parameter)</param>
        /// <param name="postamble">Text after the code block (output parameter)</param>
        /// <returns>The extracted JSON or the original text if no code block is found</returns>
        public static string ExtractJsonFromMarkdown(string text, out string preamble, out string postamble)
        {
            preamble = string.Empty;
            postamble = string.Empty;
            
            if (string.IsNullOrEmpty(text))
                return text;
                
            // Find Markdown code blocks with optional language specifier
            var codeBlockMatch = Regex.Match(text, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.Singleline);
            if (!codeBlockMatch.Success)
                return text;
                
            // Extract the content
            var jsonContent = codeBlockMatch.Groups[1].Value.Trim();
            
            // Extract text before and after the code block
            preamble = text.Substring(0, codeBlockMatch.Index).Trim();
            
            int postambleStart = codeBlockMatch.Index + codeBlockMatch.Length;
            if (postambleStart < text.Length)
            {
                postamble = text.Substring(postambleStart).Trim();
            }
            
            return jsonContent;
        }
        
        /// <summary>
        /// Check if text contains a JSON code block
        /// </summary>
        /// <param name="text">Text to check</param>
        /// <returns>True if the text contains a markdown code block</returns>
        public static bool ContainsCodeBlock(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
                
            return Regex.IsMatch(text, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.Singleline);
        }
    }
}