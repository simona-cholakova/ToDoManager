using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using TodoApi.Models;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Connectors.Google;
using WebApplication2.Services;

namespace TodoApi.Plugins
{
    public class FilePlugin
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TodoContext _context;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly TodoService _todoService;
    
    public FilePlugin(IServiceProvider serviceProvider, TodoContext context, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _serviceProvider = serviceProvider;
        _context = context;
        _embeddingGenerator = embeddingGenerator;
    }

    [KernelFunction, Description("Reads the content of a file stored on disk by name. Useful when a user references a file in their question or something related to the file")]
    
    public async Task<string> RetrieveFileFromDatabaseAsync(string fileName, int maxSize = 5000)
    {
        Console.WriteLine($"Database file function was invoked with: {fileName}");

        var file = await _context.FileRecords
            .FirstOrDefaultAsync(f => f.FileName == fileName);

        if (file == null)
        {
            return $"File '{fileName}' not found in database.";
        }

        return file.Content.Length <= maxSize
            ? file.Content
            : file.Content.Substring(0, maxSize);
    }
}
}
