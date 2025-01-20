public class ModelMetadata
{
    public string Name { get; set; }
    public string Architecture { get; set; }
    public int ContextWindowSize { get; set; }
    public string Quantization { get; set; }
    public long ParameterCount { get; set; }
    public long MemoryRequirements { get; set; }
    public bool SupportsGPU { get; set; }

    public string GetFormattedInfo() => 
        $"Model: {Name}\nArchitecture: {Architecture}\nContext Window: {ContextWindowSize}\n" +
        $"Quantization: {Quantization}\nParameters: {ParameterCount}\n" +
        $"Memory Required: {MemoryRequirements}MB\nGPU Support: {SupportsGPU}";
}
