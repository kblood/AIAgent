using LLama.Common;
using LLama;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AIAgentTest.API_Clients
{
    

    public class LlamaClient : IDisposable
    {
        private LLamaWeights _model;
        private InteractiveExecutor _executor;

        //public ChatSession chatSession 
        //{ 
        //    get { return new ChatSession(_executor); }
        //}

        private ChatSession _chatSession;

        public ChatSession ChatSession => _chatSession;

        public LlamaClient(string modelPath, int contextSize = 2048, int gpuLayerCount = 32)
        {
            var modelParams = new ModelParams(modelPath)
            {
                ContextSize = (uint)contextSize,
                GpuLayerCount = gpuLayerCount,
                //Seed = 1337,
                MainGpu = 0,
                SplitMode = LLama.Native.GPUSplitMode.None,
                //EmbeddingMode = false,
                UseMemoryLock = true,
                UseMemorymap = true,
                Threads = (int)Math.Max(Environment.ProcessorCount - 1, 1),
                BatchSize = 512,
                BatchThreads = (int?)Environment.ProcessorCount
            };

            _model = LLamaWeights.LoadFromFile(modelParams);
            _executor = new InteractiveExecutor(_model.CreateContext(modelParams));
            _chatSession = new ChatSession(_executor);
        }

        public async Task<string> Chat(string userMessage)
        {
            var inferenceParams = new InferenceParams
            {
                //Temperature = 0.7f,
                //TopK = 40,
                //TopP = 0.95f,
                //RepeatPenalty = 1.1f,
                MaxTokens = 2048  // Set a high value, we'll use custom stopping logic
            };

            // Add the user message to the chat history
            var userChatMessage = new ChatHistory.Message(AuthorRole.User, userMessage);
            //_chatSession.AddMessage(userChatMessage);

            var output = string.Empty;
            var completionPattern = new Regex(@"(\.|\!|\?)\s*$");
            var tokenCount = 0;
            var maxTokensWithoutCompletion = 100;  // Adjust as needed

            await foreach (var response in _chatSession.ChatAsync(userChatMessage,inferenceParams))
            {
                output += response;
                tokenCount++;

                if (completionPattern.IsMatch(output) || tokenCount > maxTokensWithoutCompletion)
                {
                    break;
                }
            }

            // Add the assistant's response to the chat history
            var assistantChatMessage = new ChatHistory.Message(AuthorRole.Assistant, output.Trim());
            _chatSession.AddMessage(assistantChatMessage);

            return output.Trim();
        }
        /*
        public async Task<string> Chat(string userMessage)
        {
            var inferenceParams = new InferenceParams
            {
                Temperature = 0.7f,
                TopK = 40,
                TopP = 0.95f,
                RepeatPenalty = 1.1f,
                MaxTokens = 2048  // Set a high value, we'll use custom stopping logic
            };

            var message = new ChatHistory.Message(AuthorRole.User, userMessage);

            var output = string.Empty;
            var completionPattern = new Regex(@"(\.|\!|\?)\s*$");
            var tokenCount = 0;
            var maxTokensWithoutCompletion = 100;  // Adjust as needed

            await foreach (var response in _chatSession.ChatAsync(message, inferenceParams))
            {
                output += response;
                tokenCount++;

                if (completionPattern.IsMatch(output) || tokenCount > maxTokensWithoutCompletion)
                {
                    break;
                }
            }

            return output.Trim();
        }*/

        // Method to run inference based on a prompt
        public async Task<string> RunInference(string prompt, int maxTokens = 256, List<string> antiPrompts = null)
        {
            // Set up inference parameters
            var inferenceParams = new InferenceParams
            {
                MaxTokens = maxTokens,
                AntiPrompts = antiPrompts ?? new List<string>()
            };

            // Execute the inference
            var output = string.Empty;
            await foreach (var text in _executor.InferAsync(prompt, inferenceParams))
            {
                output += text;
            }

            return output;
        }

        //public async Task<string> Chat(string userMessage, int maxTokens = 128)
        //{
        //    var inferenceParams = new InferenceParams
        //    {
        //        MaxTokens = maxTokens,
        //        Temperature = 0.7f,
        //        TopK = 40,
        //        TopP = 0.95f,
        //        RepeatPenalty = 1.1f
        //    };

        //    var message = new ChatHistory.Message(AuthorRole.User, userMessage);

        //    var output = string.Empty;
        //    await foreach (var response in _chatSession.ChatAsync(message, inferenceParams))
        //    {
        //        output += response;
        //    }

        //    return output;
        //}

        /*
        // Method to simulate a conversation with memory of the chat
        public async Task<string> Chat(string userMessage, ChatSession chatSession, int maxTokens = 128)
        {
            var inferenceParams = new InferenceParams
            {
                MaxTokens = maxTokens,
                AntiPrompts = new List<string> { "User:" } // Stop generation when user prompt is detected again
            };

            //chatSession.AddMessage(AuthorRole.User, userMessage);
            var result = await chatSession.AddAndProcessUserMessage(userMessage);

            var output = string.Empty;
            await foreach (var response in chatSession.ChatAsync(result.History.Messages.First()))
            //_executor
            //Chat(inferenceParams))
            {
                output += response;
            }

            chatSession.AddAndProcessAssistantMessage(output);
            return output;
        }*/

        // Dispose method to clean up resources
        public void Dispose()
        {
            _model?.Dispose();
        }
    }
}
