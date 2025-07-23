using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using TodoApi.Models;
using Pgvector.EntityFrameworkCore;
using WebApplication2.Services;
using Seq.Api;
using Seq.Api.Model.Events;

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
        public async Task<string> searchFileContent(string query)
        {
            Console.WriteLine("Function invoked yay!");

            var embedding = await _embeddingGenerator.GenerateAsync(query);
            var queryVector = new Pgvector.Vector(embedding.Vector);

            // Only consider chunks that have been clustered
            var closestChunk = await _context.FileChunks
                .AsNoTracking()
                .Where(c => c.ClusterID != null)
                .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
                .FirstOrDefaultAsync();

            if (closestChunk == null)
                return "No clustered chunks available in the database.";

            int targetCluster = closestChunk.ClusterID!.Value;
            Console.WriteLine($"Closest cluster: {targetCluster}");

            var matches = await _context.FileChunks
                .Include(c => c.FileRecord)
                .Where(c => c.ClusterID == targetCluster)
                .Select(c => new
                {
                    c.FileRecord.FileName,
                    c.PageNumber,
                    c.Content,
                    Distance = c.Embedding!.CosineDistance(queryVector)
                })
                .OrderBy(c => c.Distance)
                .Where(c => c.Distance < 0.4)
                .Take(5)
                .ToListAsync();

            if (!matches.Any())
                return $"No relevant file chunks found in cluster {targetCluster}.";

            return string.Join("\n\n", matches.Select(m =>
                $"From {m.FileName}, page {m.PageNumber}:\n{m.Content}"
            ));
        }

        
    }
}



