using LLama.Common;
using LLama.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AIAgentTest.Services;
using LLama;

namespace AIAgentTest.Examples
{
    public class LlavaInteractiveModeExecute
    {
        public static async Task Run(string multiModalProj, string modelPath, string inputImage)
        {
            try
            {
                Console.WriteLine("Starting LlavaInteractiveModeExecute.Run");
                Console.WriteLine($"multiModalProj: {multiModalProj}");
                Console.WriteLine($"modelPath: {modelPath}");
                Console.WriteLine($"inputImage: {inputImage}");

                //const int maxTokens = 1024;
                const int maxTokens = 4096;

                var prompt = $"{{{inputImage}}}\nUSER:\nProvide a full description of the image.\nASSISTANT:\n";

                Console.WriteLine("Creating ModelParams");
                var parameters = new ModelParams(modelPath);
                parameters.ContextSize = 4096;
                parameters.GpuLayerCount = 32;
                parameters.Seed = 1337;
                parameters.MainGpu = 0;
                parameters.SplitMode = GPUSplitMode.None;
                parameters.UseMemoryLock = true;
                parameters.UseMemorymap = true;
                parameters.Threads = (uint)Math.Max(Environment.ProcessorCount - 1, 1);
                parameters.BatchSize = 512;
                parameters.BatchThreads = (uint?)Environment.ProcessorCount;

                Console.WriteLine("Loading LLamaWeights");
                using var model = LLamaWeights.LoadFromFile(parameters);
                Console.WriteLine("Creating context");
                using var context = model.CreateContext(parameters);

                Console.WriteLine("Loading LLavaWeights");
                using var clipModel = LLavaWeights.LoadFromFile(multiModalProj);

                Console.WriteLine("Creating InteractiveExecutor");
                var ex = new InteractiveExecutor(context, clipModel);

                Console.WriteLine($"The executor has been enabled. Max tokens: {maxTokens}, Context size: {parameters.ContextSize}");
                Console.WriteLine("To send an image, enter its filename in curly braces, like this {c:/image.jpg}.");

                var inferenceParams = new InferenceParams() { Temperature = 0.1f, AntiPrompts = new List<string> { "\nUSER:" }, MaxTokens = maxTokens };

                Console.WriteLine("Starting RunInteractiveLoopAsync");
                await RunInteractiveLoopAsync(ex, prompt, inferenceParams);
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occurred in Run method: {e.Message}");
                Console.WriteLine($"Stack trace: {e.StackTrace}");
            }
            Console.WriteLine("Exiting LlavaInteractiveModeExecute.Run");
        }

        private static async Task RunInteractiveLoopAsync(InteractiveExecutor ex, string initialPrompt, InferenceParams inferenceParams)
        {
            try
            {
                string prompt = initialPrompt;
                do
                {
                    //Console.WriteLine($"Current prompt: {prompt}");
                    var imageMatches = Regex.Matches(prompt, "{([^}]*)}").Select(m => m.Value);
                    var imageCount = imageMatches.Count();
                    var hasImages = imageCount > 0;

                    //Console.WriteLine($"Has images: {hasImages}, Image count: {imageCount}");
                    if (hasImages)
                    {
                        var imagePathsWithCurlyBraces = Regex.Matches(prompt, "{([^}]*)}").Select(m => m.Value);
                        var imagePaths = Regex.Matches(prompt, "{([^}]*)}").Select(m => m.Groups[1].Value).ToList();

                        List<byte[]> imageBytes = new List<byte[]>();
                        foreach (var imagePath in imagePaths)
                        {
                            Console.WriteLine($"Processing image: {imagePath}");
                            if (File.Exists(imagePath))
                            {
                                imageBytes.Add(await File.ReadAllBytesAsync(imagePath));
                                Console.WriteLine($"Successfully read image: {imagePath}");
                                /*
                                try
                                {
                                    ImageService.DisplayImageWithBox(imagePath, new SixLabors.ImageSharp.Rectangle(0,0,100,100));
                                    Console.WriteLine($"Image displayed: {imagePath}");
                                }
                                catch (Exception displayEx)
                                {
                                    Console.WriteLine($"Error displaying image {imagePath}: {displayEx.Message}");
                                }
                                */
                            }
                            else
                            {
                                Console.WriteLine($"Image file not found: {imagePath}");
                            }
                        }
                        
                        Console.WriteLine("Clearing KV cache");
                        ex.Context.NativeHandle.KvCacheRemove(LLamaSeqId.Zero, -1, -1);

                        int index = 0;
                        foreach (var path in imagePathsWithCurlyBraces)
                        {
                            prompt = prompt.Replace(path, index++ == 0 ? "<image>" : "");
                        }

                        Console.WriteLine("Updated prompt after image processing: " + prompt);

                        Console.WriteLine("Clearing and adding images to executor");
                        ex.Images.Clear();
                        var imageRawBytes = File.ReadAllBytes(imagePaths.First());
                        ex.Images.Add(imageRawBytes);

                        //foreach (var image in imageBytes)
                        //{
                        //    ex.Images.Add(image);
                        //}
                        //var cap = ex.Images.Capacity;
                        //var count = ex.Images.Count;
                        //var test = ex.IsMultiModal;
                        //var test2 = ex.Images.First();
                        await ex.PrefillPromptAsync(prompt);
                    }
                    //prompt = "<image>\nUSER: Describe the image, its name, aspect ratio and how many images you have.";
                    Console.WriteLine(prompt);
                    Console.WriteLine("Model response:");
                    await foreach (var text in ex.InferAsync(prompt, inferenceParams))
                    {
                        Console.Write(text);
                    }
                    Console.WriteLine();
                    Console.Write("Your input: ");
                    prompt = Console.ReadLine();
                    Console.WriteLine();

                    if (prompt != null && prompt.Equals("/exit", StringComparison.OrdinalIgnoreCase))
                        break;

                } while (true);
            }
            catch (Exception e)
            {
                Console.WriteLine($"An error occurred in RunInteractiveLoopAsync: {e.Message}");
                Console.WriteLine($"Stack trace: {e.StackTrace}");
            }
            Console.WriteLine("Exiting RunInteractiveLoopAsync");
        }
    }
}