using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using System.Linq;
using AIAgentTest.Services.MCP;

namespace AIAgentTest.API_Clients.MCP
{
    /// <summary>
    /// Extension methods for implementing GenerateWithFunctionsAsync
    /// </summary>
    public static class GenerateWithFunctionsAsyncExtension
    {
        /// <summary>
        /// Converts a FunctionDefinition to a ToolDefinition
        /// </summary>
        /// <param name="function">Function definition to convert</param>
        /// <returns>Tool definition</returns>
        public static ToolDefinition ConvertFunctionToTool(FunctionDefinition function)
        {
            // Convert parameters to input schema
            var properties = new Dictionary<string, object>();
            var requiredParams = new List<string>();
            
            foreach (var param in function.Parameters)
            {
                properties[param.Key] = new Dictionary<string, object>
                {
                    { "type", param.Value.Type.ToLower() },
                    { "description", param.Value.Description }
                };
                
                if (param.Value.Required)
                {
                    requiredParams.Add(param.Key);
                }
            }
            
            // Create input schema
            var inputSchema = new Dictionary<string, object>
            {
                { "type", "object" },
                { "properties", properties },
                { "required", requiredParams }
            };
            
            // Create output schema (generic)
            var outputSchema = new Dictionary<string, object>
            {
                { "type", "object" },
                { "description", "Function result" }
            };
            
            return new ToolDefinition
            {
                Name = function.Name,
                Description = function.Description,
                Input = inputSchema,
                Output = outputSchema,
                ToolType = "function",
                Tags = new string[] { "function" }
            };
        }
        
        /// <summary>
        /// Implementation of GenerateWithFunctionsAsync for IMCPLLMClient
        /// </summary>
        /// <param name="client">MCP client</param>
        /// <param name="prompt">Prompt</param>
        /// <param name="model">Model name</param>
        /// <param name="functions">Function definitions</param>
        /// <returns>Generated response</returns>
        public static async Task<string> GenerateWithFunctionsAsyncImpl(
            this IMCPLLMClient client,
            string prompt, 
            string model, 
            List<FunctionDefinition> functions)
        {
            // Convert FunctionDefinitions to ToolDefinitions and use MCP
            var tools = functions.Select(f => ConvertFunctionToTool(f)).ToList();
            
            var mcpResponse = await client.GenerateWithMCPAsync(prompt, model, tools);
            
            // Format the result to match what the existing code expects from function calls
            if (mcpResponse.Type == "tool_use")
            {
                return JsonSerializer.Serialize(new { 
                    name = mcpResponse.Tool, 
                    arguments = JsonSerializer.Serialize(mcpResponse.Input) 
                });
            }
            else
            {
                return mcpResponse.Text;
            }
        }
    }
}
