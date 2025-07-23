using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace WebApplication2.Services;

public class AIOptionService
{
    private readonly Kernel _kernel;

    public AIOptionService(Kernel kernel)
    {
        _kernel = kernel;
    }

    public IChatCompletionService GetService(string provider)
    {
        return provider.ToLower() switch
        {
            "openai" => _kernel.GetRequiredService<IChatCompletionService>("OpenAI.ChatCompletion"),
            "gemini" => _kernel.GetRequiredService<IChatCompletionService>("GoogleAIGemini"),
            _ => throw new ArgumentException($"Unsupported provider '{provider}'")
        };
    }
}