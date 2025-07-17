using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using TodoApi.Models;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Connectors.Google;
using Pgvector.EntityFrameworkCore;
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
        

        [KernelFunction, Description("Searches if the user's prompt can be found in the files from the database")]
        public async Task<string> SearchFilesByMeaningAsync(string query)
        {
            Console.WriteLine("Function invoked yay!");

            var embedding = await _embeddingGenerator.GenerateAsync(query);
            var vector = new Pgvector.Vector(embedding.Vector);

            // Retrieve all files with their cosine distances
            var allMatches = await _context.FileRecords
                .Select(f => new
                {
                    f.FileName,
                    f.Content,
                    Distance = f.Embedding!.CosineDistance(vector)
                })
                .OrderBy(f => f.Distance)
                .ToListAsync();

            // Filter by distance threshold (you can adjust 0.4 as needed)
            var matchingFiles = allMatches
                .Where(f => f.Distance < 0.4)
                .Take(5)
                .ToList();

            if (matchingFiles.Count == 0)
                return "No relevant files found.";

            return string.Join("\n\n", matchingFiles.Select(f =>
                $"From {f.FileName}:\n{f.Content}"
            ));
        }

    }
}
