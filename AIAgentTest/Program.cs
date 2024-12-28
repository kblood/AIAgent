using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using AIAgentTest.API_Clients;
using AIAgentTest.Services;
using OpenQA.Selenium.Interactions.Internal;
using SixLabors.ImageSharp.PixelFormats;
using System.Numerics;
using SixLabors.ImageSharp;
using System.Diagnostics;
using AIAgentTest.Examples;
using System.IO;

namespace AIAgentFramework
{
    class Program
    {
        public static string ExtractPlainTextResponse(string jsonLines)
        {
            var responseBuilder = new StringBuilder();

            foreach (var line in jsonLines.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(line))
                    {
                        JsonElement root = doc.RootElement;
                        if (root.TryGetProperty("response", out JsonElement responseElement))
                        {
                            responseBuilder.Append(responseElement.GetString());
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Failed to parse JSON: {ex.Message}");
                }
            }

            return responseBuilder.ToString();
        }

        public async Task SaveToFileAsync(object file, string filePath)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(file, options);
            await File.WriteAllTextAsync(filePath, json);
        }

        static async Task Main(string[] args)
        {
            //var LMStudioAgemt = new LMStudioClient();

            //var installedmodels = LMStudioAgemt.ListInstalledModels();
            //var models = await LMStudioAgemt.ListLoadedModelsAsync();
            var beforeInfo = await NvidiaSmiUtility.GetGPUInfoAsync();
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var ollamaAgent = new OllamaClient();
            var availableModels = await ollamaAgent.GetAvailableModelsAsync();
            var modelToUse = availableModels.Where(m => m.Name.Contains("falcon3:10b")).MaxBy(m => m.Size); //marco-o1:latest  falcon3:10b
            //var modelToUse = availableModels.Where(m => m.Name.Contains("x/llama3.2-vision:11b")).MaxBy(m => m.Size); //marco-o1:latest

            //var modelToUse = availableModels.Where(m => m.Name.Contains("llama3.2:1b")).MaxBy(m => m.Size);
            //

            await ChatPrompt(modelToUse);

            //await ImageTests(ollamaAgent, availableModels.Where(m => m.Name.Contains("x/llama3.2-vision:11b")).MaxBy(m => m.Size));
            return;
            /*
            string imagePath = "E:/Billeder/LLM Test/MtGArena20240918.jpg";
            //string imagePath = "E:\\Billeder\\LLM Test\\gregmad.jpg";
            var path = "C:\\LLM\\GGUF\\MiniCPM-V-2.6";
            //var visionModel = "ggml-model-Q4_0.gguf";
            //var mmproj = "mmproj-model-f16.gguf";
            var visionModel = "MiniCPM-V-2_6-IQ4_XS.gguf";
            var mmproj = "mmproj-MiniCPM-V-2_6-f16.gguf";

            //await LlavaInteractiveModeExecute.Run(Path.Combine(path, mmproj), Path.Combine( path, visionModel), imagePath);

            await ChatTests();
            Console.WriteLine("Total elapsed time: " + stopwatch.Elapsed.TotalSeconds);
            */
            //aratan / vision:latest
            //await ImageTests(ollamaAgent, modelToUse);

            //ImageService.OpenImageWithDefaultViewer($"{Path.GetFileNameWithoutExtension(imagePath)}_with_box.png");

            /*
            var webservice = new UnifiedSearchService();
            string topic = "latest advancements in AI";
            var searchResults = await webservice.PerformWebSearchList(topic);
            webservice.Dispose();

            WebScraper webScraper = new WebScraper();
            foreach (var result in searchResults)
            {
                result.Content = await webScraper.ScrapeWebpage(result.Url);
                var trimmedContent = await ollamaAgent.GenerateTextResponseAsync($"Your answer should be formatted as only the information about the topic, no explanation or greetings. Trim this text down to only be the text relevant for the topic: {topic}\n{result.Content}");
                result.Content = trimmedContent;
                //result.Links = webScraper.ExtractLinks(result.Url);
            }

            var dirinfo = Directory.CreateDirectory(topic);
            JsonSerializerUtility.SaveToFileAsync(searchResults, Path.Combine(dirinfo.FullName, "searchResults.json"));

            var formattedResults = UnifiedSearchService.FormatResultsForLLM(searchResults);

            var response = await ollamaAgent.GenerateTextResponseAsync($"Provide your answer as only a number: Based on the following search results, which do you think we should open to know more about the advancements in AI. Name the number of the result only:\n\n {formattedResults}", modelToUse.Name);
            //var response = await ollamaAgent.GenerateTextResponseAsync($"Based on the following search results, provide a summary of the latest advancements in AI:\n\n {formattedResults}", modelToUse.Name);
            JsonSerializerUtility.SaveToFileAsync(formattedResults, Path.Combine(dirinfo.FullName, "formattetResults.json"));
            JsonSerializerUtility.SaveToFileAsync(response, Path.Combine(dirinfo.FullName, "summary.json"));
            */

            var afterUsage = await NvidiaSmiUtility.GetGPUInfoAsync();

        }

        private static async Task ChatPrompt(ModelInfo? model)
        {
            var gpuBeforeInfo = await NvidiaSmiUtility.GetGPUInfoAsync();
            // Ollama tests
            var ollamaAgent = new OllamaClient();
            //Console.WriteLine("Ollama responses:");
            //var availableModels = await ollamaAgent.GetAvailableModelsAsync();
            //ModelInfo? model = availableModels.Where(m => m.Name.ToLower().Contains(modelName)).MaxBy(m => m.Size);
            Console.WriteLine($"Model: {model.Name}, Family: {model.Details.Family}");
            Console.WriteLine($"To exist the prompt, type exit.");
            var startTimeOllamaAgent = DateTime.Now;
            string prompt = "";
            prompt = Console.ReadLine();
            while (!prompt.ToLower().Contains("exit"))
            {
                var startTime = DateTime.Now;
                var ollamaResponse = await ollamaAgent.GenerateTextResponseAsync(prompt, model.Name);
                var endTime = DateTime.Now;
                //Console.WriteLine($"Question: {question}");
                Console.WriteLine($"Ollama response (took {(endTime - startTime).TotalSeconds} seconds):");
                Console.WriteLine(ollamaResponse);
                Console.WriteLine();
                //var gpuInfo = await NvidiaSmiUtility.GetGPUInfoAsync();
                prompt = Console.ReadLine();
            }
        }

        private static async Task ChatTests()
        {
            string modelPath = "d:\\LLM\\LLMStudio\\lmstudio-community\\Llama-3.2-3B-Instruct-GGUF\\Llama-3.2-3B-Instruct-Q4_K_M.gguf";

            var startTimellamaClient = DateTime.Now;

            var questions = new[]
            {
                "Write a short story about: lemmings in Danish",
                "Explain the concept of quantum entanglement",
                "What are the main ingredients in a traditional paella?"
            };

            using (var llamaClient = new LlamaClient(modelPath))
            {
                // LlamaClient tests
                Console.WriteLine("LlamaClient responses:");
                foreach (var question in questions)
                {
                    var startTime = DateTime.Now;
                    var llamaResponse = await llamaClient.Chat(question);
                    var endTime = DateTime.Now;
                    Console.WriteLine($"Question: {question}");
                    Console.WriteLine($"Reponse: ");
                    Console.WriteLine(llamaResponse);
                    Console.WriteLine($"LlamaClient response (took {(endTime - startTime).TotalSeconds} seconds):");
                    Console.WriteLine();
                }
            }
            var endTimellamaClient = DateTime.Now;
            Console.WriteLine($"LlamaClient total elapsed time: {(endTimellamaClient - startTimellamaClient).TotalSeconds} seconds");

            // Ollama tests
            var ollamaAgent = new OllamaClient();
            Console.WriteLine("Ollama responses:");
            var availableModels = await ollamaAgent.GetAvailableModelsAsync();
            var model = availableModels.Where(m => m.Name.Contains("llama3.2:latest")).MaxBy(m => m.Size);
            var startTimeOllamaAgent = DateTime.Now;
            //foreach (var model in availableModels.Where(m => m.Name.Contains("llama3.2:latest")).MaxBy(m => m.Size))
            {
                Console.WriteLine($"Model: {model.Name}, Family: {model.Details.Family}");
                foreach (var question in questions)
                {
                    var startTime = DateTime.Now;
                    var ollamaResponse = await ollamaAgent.GenerateTextResponseAsync(question, model.Name);
                    var endTime = DateTime.Now;
                    Console.WriteLine($"Question: {question}");
                    Console.WriteLine($"Ollama response (took {(endTime - startTime).TotalSeconds} seconds):");
                    Console.WriteLine(ollamaResponse);
                    Console.WriteLine();
                }
            }
            var endTimeOllamaAgent = DateTime.Now;
            Console.WriteLine($"Ollama total elapsed time: {(endTimeOllamaAgent - startTimeOllamaAgent).TotalSeconds} seconds");
        }

        private static async Task ImageTests(OllamaClient ollamaAgent, ModelInfo? modelToUse)
        {
            //string imagePath = "E:\\Billeder\\Hjemmet\\LejlighedAarestrupsvej38stth.jpg";
            //string imagePath = "e:\\Billeder\\Rene\\Fede Baggrunde\\IMG_3131.JPG";
            //string imagePath = "E:\\Billeder\\LLM Test\\gregmad.jpg";
            //string imagePath = "E:\\Billeder\\LLM Test\\screenshotDiscord.png";
            string imagePath = "E:\\Billeder\\LLM Test\\MtGArena20240918.png";
            //string imagePath = "E:\\Billeder\\LLM Test\\MtGArena20240918.jpg";
            //var imagedescription = await ollamaAgent.GenerateResponseWithImageAsync($"List all items you can see in this image", imagePath, modelToUse.Name);
            //var imagedescription = await ollamaAgent.GenerateResponseWithImageAsync($"Where in this image is the Swamp land card? Describe its pixel coordinates. .", imagePath, modelToUse.Name);

            //var imagedescription = await ollamaAgent.GenerateResponseWithImageAsync($"Answer in this format: format it as <box>x1 y1 x2 y2</box>. Make a box of x y pixel coordinates that goes along the edges of this image.", imagePath, modelToUse.Name);
            //var imageRect = ImageService.ParseCoordinates(imagedescription);
            //ImageService.OpenImageWithDefaultViewer($"{Path.GetFileNameWithoutExtension(imagePath)}_with_box.png");

            //var imagedescription = await ollamaAgent.GenerateResponseWithImageAsync($"Answer in this format where x1 and y1 is the top left and the x2 and y2 are the bottom right in pixels: <box>x1 y1 x2 y2</box>. Where in this image is Tergrid card?", imagePath, modelToUse.Name);

            string objectDescription = "the swamp card";
            //objectDescription = "the meathook card";
            //objectDescription = "Keep 7 button";

            var size = ImageService.GetImageSize(imagePath);
            var prompt = $"This image is {size.X}x{size.Y} pixels. Please identify the pixel position of {objectDescription} in the image. \r\nProvide the coordinates of the top-left and bottom-right corners of a bounding box around the object.\r\nExpress the coordinates as pixel values.";
            prompt = "How many cards do you see in this image? Split the image up in squares, 12 horizontal and 6 vertical. In what square would you place each of the 7 cards?";
            Console.WriteLine(prompt);
            //$"\r\nExplain your reasoning for choosing these coordinates.";
            while (!prompt.Contains("exit"))
            {
                var imagedescription = await ollamaAgent.GenerateResponseWithImageAsync(prompt, imagePath, modelToUse.Name);
                Console.WriteLine(imagedescription);
                Console.WriteLine("Enter a new prompt or type 'exit' to quit.");
                prompt = Console.ReadLine();
            }


            /*
            Rectangle rectangle = new Rectangle();
            while (rectangle.X == 0)
            {
                try
                {
                    rectangle = ImageService.ParseCoordinates(imagedescription, (int)size.X, (int)size.Y);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing coordinates: {ex.Message}");
                    imagedescription = await ollamaAgent.GenerateResponseWithImageAsync(prompt, imagePath, modelToUse.Name);
                    //[0.36, 0.26, 0.52, 0.47] swamp
                    //[0.0, 0.25, 0.19, 0.36] swamp
                    //[0.34, 0.26, 0.49, 0.48] meathook
                    //[0.24, 0.3, 0.39, 0.51] meathook
                }
            }
            ImageService.DisplayImageWithBox(imagePath, imagedescription);
            */
        }
    }
}