using System.Text;
using System.Text.Json;
using Accord.MachineLearning;
using Accord.Math.Distances;
using Microsoft.Extensions.AI;
using TodoApi.Models;

namespace WebApplication2.Services
{
    public class FileService
    {
        private readonly TodoContext _context;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

        public FileService(TodoContext context, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
        {
            _context = context;
            _embeddingGenerator = embeddingGenerator;
        }
        
        public static string FlattenJson(JsonElement element)
        {
            var sb = new StringBuilder();

            void Recurse(JsonElement el)
            {
                switch (el.ValueKind)
                {
                    case JsonValueKind.Object:
                        foreach (var property in el.EnumerateObject())
                        {
                            sb.Append(property.Name).Append(": ");
                            Recurse(property.Value);
                            sb.AppendLine();
                        }
                        break;

                    case JsonValueKind.Array:
                        foreach (var item in el.EnumerateArray())
                        {
                            Recurse(item);
                            sb.AppendLine();
                        }
                        break;

                    case JsonValueKind.String:
                        sb.Append(el.GetString());
                        break;

                    case JsonValueKind.Number:
                        sb.Append(el.GetRawText());
                        break;

                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        sb.Append(el.GetBoolean());
                        break;

                    case JsonValueKind.Null:
                        sb.Append("null");
                        break;
                }
            }

            Recurse(element);
            return sb.ToString();
        }

        public static List<string> SplitTextIntoChunks(string text, int maxChars)
        {
            var chunks = new List<string>();

            for (int i = 0; i < text.Length; i += maxChars)
            {
                int length = Math.Min(maxChars, text.Length - i);
                chunks.Add(text.Substring(i, length));
            }

            return chunks;
        }

        public void KMeansClustering(FileRecord uploadedFile)
        {
            var chunksList = uploadedFile.Chunks.ToList();

            if (chunksList.Count == 0)
                return;

            // Convert embeddings to double[][] for Accord
            double[][] embeddingsForClustering = chunksList
                .Select(c => c.Embedding.ToArray().Select(f => (double)f).ToArray())
                .ToArray();

            int numberOfClusters = Math.Min(100, chunksList.Count);

            // Initialize and train K-Means
            var kmeans = new KMeans(k: numberOfClusters)
            {
                Distance = new Euclidean(),
                MaxIterations = 100
            };

            var clusters = kmeans.Learn(embeddingsForClustering);
            int[] assignments = clusters.Decide(embeddingsForClustering);

            // Assign cluster info to each chunk
            for (int i = 0; i < chunksList.Count; i++)
            {
                chunksList[i].ClusterID = assignments[i];
                chunksList[i].ClusterMethod = $"K-Means (k={numberOfClusters})";
            }

            // Save changes to the database
            _context.SaveChanges();
        }
        
        public async Task ProcessBatch(List<string> batch, FileRecord fileRecord)
        {
            var embeddings = await _embeddingGenerator.GenerateAndZipAsync(batch);

            foreach (var embedding in embeddings)
            {
                ReadOnlyMemory<float> embeddingMemory = embedding.Embedding.Vector;
                Pgvector.Vector vector = new Pgvector.Vector(embeddingMemory);

                fileRecord.Chunks.Add(new FileChunk
                {
                    Content = embedding.Value,
                    Embedding = vector
                });
            }
        }
        
        public int EstimateTokenCount(string text)
        {
            return (int)(text.Length / 4.0); // very rough estimate: 1 token ≈ 4 characters
        }


    }
}
