using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Extensions.AI;
using TodoApi.Models;
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
        
        [KernelFunction, Description("Searches if the user's prompt can be found in the files from the database. Always read from the most similar file.")]
        public async Task<List<FileChunk>> searchFileContent(string query)
        {
            Console.WriteLine("Function invoked yay!");

            var embedding = await _embeddingGenerator.GenerateAsync(query);
            var queryVector = new Pgvector.Vector(embedding.Vector);

            // Only consider chunks that have been clustered
            var closestChunk = _context.FileChunks
                .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
                .First();

            var cluster = _context.FileChunks.Where(f => f.ClusterID == closestChunk.ClusterID).ToList();

            return cluster;
        }

        
    }
}



