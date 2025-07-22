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
        private readonly HttpClient client = new HttpClient();
        private readonly SeqConnection _conn = new SeqConnection("http://localhost:32768", "UtnUVhWx91hv5x9xGIBz");

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
            var vector = new Pgvector.Vector(embedding.Vector);

            // Retrieve all files with their cosine distances
            var matches = await _context.FileChunks
                .Include(c => c.FileRecord)
                .Select(c => new
                {
                    c.FileRecord.FileName,
                    c.PageNumber,
                    c.Content,
                    Distance = c.Embedding!.CosineDistance(vector)
                })
                .OrderBy(c => c.Distance)
                .Where(c => c.Distance < 0.4)
                .Take(5)
                .ToListAsync();

            if (!matches.Any())
                return "No relevant file chunks found.";

            return string.Join("\n\n", matches.Select(m =>
                $"From {m.FileName}, page {m.PageNumber}:\n{m.Content}"
            ));
        }
        
        [KernelFunction("GetLogs")]
        [Description("Fetch the event from SEQ using the provided filters")]
        public async Task<IEnumerable<string>> QueryLogs(string filters)
        {
            Console.WriteLine(filters);    
            var res = _conn.Events.EnumerateAsync(filter: filters, render: true);        
            List<string> logs = new List<string>();
            await foreach (var evt in res)
            {
                Console.WriteLine(evt.RenderedMessage);        
                logs.Add(evt.RenderedMessage);
            }
            return logs;
        }
    }
}



