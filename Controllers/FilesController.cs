using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using UglyToad.PdfPig;
using Pgvector;
using TodoApi.Utils;
using UglyToad.PdfPig.Content;
using WebApplication2.Services;


namespace TodoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly TodoContext _db;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
        private readonly FileService _fileService;
        private readonly UserManager<User> _userManager;
        private readonly FileRecord _fileRecord;

        public FilesController(
            TodoContext db,
            UserManager<User> userManager, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator) 
        {
            _db = db;
            _userManager = userManager; 
            _embeddingGenerator = embeddingGenerator;
            _fileService = new FileService(_db, embeddingGenerator);
        }
        

    [HttpPost("add-file")]
    public async Task<IActionResult> AddFile(IFormFile newFile)
    {
        if (newFile == null || string.IsNullOrWhiteSpace(newFile.FileName))
            return BadRequest("Invalid file data.");

        // Check if file exists
        var exists = await _db.FileRecords.AnyAsync(f => f.FileName == newFile.FileName);
        if (exists)
            return BadRequest($"File '{newFile.FileName}' already exists.");

        var fileRecord = new FileRecord
        {
            FileName = newFile.FileName,
            Content = string.Empty
        };

        var fileChunks = new List<FileChunk>();
        var extension = Path.GetExtension(newFile.FileName).ToLowerInvariant();

        if (extension == ".pdf")
        {
            using var pdf = PdfDocument.Open(newFile.OpenReadStream());
            var fullBuilder = new StringBuilder();
            int pageNum = 1;

            foreach (Page page in pdf.GetPages())
            {
                string pageText = page.Text;
                fullBuilder.AppendLine(pageText);

                var chunks = FileService.SplitTextIntoChunks(pageText, 2000); 
                foreach (var chunk in chunks)
                {
                    var embedding = await _embeddingGenerator.GenerateAsync(chunk);
                    fileChunks.Add(new FileChunk
                    {
                        FileRecord = fileRecord,
                        PageNumber = pageNum,
                        Content = chunk,
                        Embedding = new Vector(embedding.Vector.ToArray())
                    });
                }

                pageNum++;
            }

            fileRecord.Content = fullBuilder.ToString();
        }
        else if (extension == ".json")
        {
            using var reader = new StreamReader(newFile.OpenReadStream(), Encoding.UTF8, true);
            var jsonText = await reader.ReadToEndAsync();

            try
            {
                var jsonDoc = JsonDocument.Parse(jsonText);
                var flattenedText = FileService.FlattenJson(jsonDoc.RootElement);
                var chunks = FileService.SplitTextIntoChunks(flattenedText, 2000);

                int chunkNumber = 1;
                foreach (var chunk in chunks)
                {
                    var embedding = await _embeddingGenerator.GenerateAsync(chunk);
                    fileChunks.Add(new FileChunk
                    {
                        FileRecord = fileRecord,
                        PageNumber = chunkNumber++,
                        Content = chunk,
                        Embedding = new Vector(embedding.Vector.ToArray())
                    });
                }

                fileRecord.Content = flattenedText;
            }
            catch (JsonException)
            {
                return BadRequest("Invalid JSON content.");
            }
        }else if (extension == ".jsonl")
        {
            using var stream = newFile.OpenReadStream();
            using var reader = new StreamReader(stream);
            string? line;
            List<string> lineBatch = new();
            int currentTokenCount = 0;
            const int maxTokensPerBatch = 7000;

            var fullTextBuilder = new StringBuilder(); // <-- build the full text here

            while ((line = await reader.ReadLineAsync()) != null)
            {
                string messageLine = JsonLSplitter.ExtractValuesOnly(line);
                fullTextBuilder.AppendLine(messageLine); // <-- accumulate original content

                int estimatedTokens = _fileService.EstimateTokenCount(messageLine);
                if (currentTokenCount + estimatedTokens > maxTokensPerBatch && lineBatch.Count > 0)
                {
                    await _fileService.ProcessBatch(lineBatch, fileRecord);
                    lineBatch.Clear();
                    currentTokenCount = 0;
                }

                lineBatch.Add(messageLine);
                currentTokenCount += estimatedTokens;
            }

            // process remaining
            if (lineBatch.Count > 0)
            {
                await _fileService.ProcessBatch(lineBatch, fileRecord);
            }

            fileRecord.Content = fullTextBuilder.ToString(); // <-- now set content

            _fileService.KMeansClustering(fileRecord);
        }


        else
        {
            using var reader = new StreamReader(newFile.OpenReadStream());
            var text = await reader.ReadToEndAsync();
            var chunks = FileService.SplitTextIntoChunks(text, 2000);

            int chunkNumber = 1;
            foreach (var chunk in chunks)
            {
                var embedding = await _embeddingGenerator.GenerateAsync(chunk);
                fileChunks.Add(new FileChunk
                {
                    FileRecord = fileRecord,
                    PageNumber = chunkNumber++,
                    Content = chunk,
                    Embedding = new Vector(embedding.Vector.ToArray())
                });
            }

            fileRecord.Content = text;
        }

        await _db.FileRecords.AddAsync(fileRecord);
        await _db.FileChunks.AddRangeAsync(fileChunks);
        await _db.SaveChangesAsync();
        //_fileService.KMeansClustering(fileRecord);

        return Ok($"File '{newFile.FileName}' uploaded with {fileChunks.Count} chunk(s).");
    }



        [HttpGet("get-file")]
        public async Task<IActionResult> GetFile([FromQuery] string fileName)
        {
            if (!_db.FileRecords.Any(f => f.FileName == fileName))
            {
                return BadRequest("Invalid file name.");
            }
            var file = await _db.FileRecords.FirstOrDefaultAsync(f => f.FileName == fileName);
            return Ok(file);
        }
    }
}