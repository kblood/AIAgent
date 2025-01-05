// Add FunctionCallingService.cs implementation
using System.Text.Json;

namespace AIAgentTest.Services
{
    public class FunctionDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, ParameterDefinition> Parameters { get; set; }
    }

    public class ParameterDefinition
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
    }

    public class FunctionCall
    {
        public string Name { get; set; }
        public Dictionary<string, object> Arguments { get; set; }
    }

    public class FunctionCallingService
    {
        private readonly Dictionary<string, (FunctionDefinition Definition, Func<Dictionary<string, object>, Task<string>> Handler)> _functions = new();

        public void RegisterFunction(string name, string description, Dictionary<string, ParameterDefinition> parameters, Func<Dictionary<string, object>, Task<string>> handler)
        {
            _functions[name] = (new FunctionDefinition { Name = name, Description = description, Parameters = parameters }, handler);
        }

        public string GetFunctionDefinitions()
        {
            return JsonSerializer.Serialize(_functions.Values.Select(f => f.Definition).ToList());
        }

        public async Task<string> ProcessAIResponse(string response)
        {
            try
            {
                var functionCall = JsonSerializer.Deserialize<FunctionCall>(response);
                if (functionCall?.Name != null && _functions.ContainsKey(functionCall.Name))
                {
                    var (_, handler) = _functions[functionCall.Name];
                    return await handler(functionCall.Arguments);
                }
            }
            catch (JsonException) { }
            return response;
        }
    }
}
