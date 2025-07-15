using Microsoft.SemanticKernel;
using System.ComponentModel;

public class NativeFunctions
{
    [KernelFunction]
    public async Task<string> RetrieveLocalFileAsync(string fileName, int maxSize = 5000)
    {
        string basePath = AppContext.BaseDirectory;
        string filePath = Path.Combine(basePath, fileName);
        if (!File.Exists(filePath))
        {
            return $"File '{fileName}' not found.";
        }

        string content = await File.ReadAllTextAsync(fileName);
        return content.Length <= maxSize ? content : content.Substring(0, maxSize);
    }
}