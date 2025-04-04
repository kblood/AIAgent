using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;

namespace AIAgentTest.Services
{
    /// <summary>
    /// Simple token counter utility for estimating token counts
    /// Note: This is a very rough approximation and not a proper BPE tokenizer
    /// </summary>
    public static class TokenCounterUtility
    {
        /// <summary>
        /// Estimate the number of tokens in the text
        /// </summary>
        /// <param name="text">Input text</param>
        /// <returns>Estimated token count</returns>
        public static int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Regex to split text into tokens (very simplified version of tokenization)
            // This is a rough approximation based on whitespace, punctuation, and common patterns
            
            // Step 1: Count whitespace-separated words (baseline)
            var wordCount = Regex.Matches(text, @"[\p{L}\p{N}]+").Count;
            
            // Step 2: Count punctuation and special characters which are usually separate tokens
            var punctCount = Regex.Matches(text, @"[\p{P}\p{S}]").Count;
            
            // Step 3: Account for common tokens and special sequences
            // Common tokens include: \n, numbers, etc.
            var newlineCount = Regex.Matches(text, @"\n").Count;
            
            // Final calculation: words + punctuation + special tokens
            // Apply a multiplier to account for subword tokenization (many words are split into multiple tokens)
            const double subwordMultiplier = 1.3; // Typical multiplier for English text
            
            int estimatedTokens = (int)Math.Ceiling((wordCount * subwordMultiplier) + punctCount + newlineCount);
            
            return estimatedTokens;
        }
        
        /// <summary>
        /// Estimate token count for JSON
        /// </summary>
        /// <param name="json">JSON string</param>
        /// <returns>Estimated token count</returns>
        public static int EstimateJsonTokenCount(string json)
        {
            if (string.IsNullOrEmpty(json))
                return 0;
                
            // JSON has more tokens due to structure characters
            var baseCount = EstimateTokenCount(json);
            
            // Count braces, brackets, colons, commas which are all separate tokens
            var structureCount = Regex.Matches(json, @"[{}\[\]:,]").Count;
            
            // Count quoted strings which have start/end quotes as tokens
            var quotedStrings = Regex.Matches(json, "\"([^\"\\\\]|\\\\.)*\"").Count;
            
            return baseCount + structureCount + (quotedStrings * 2);
        }
    }
}