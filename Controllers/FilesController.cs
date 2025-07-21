using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using UglyToad.PdfPig;
using Pgvector;
using UglyToad.PdfPig.Content;


namespace TodoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly TodoContext _db;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

        private readonly UserManager<User> _userManager;

        public FilesController(
            TodoContext db,
            UserManager<User> userManager, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator) 
        {
            _db = db;
            _userManager = userManager; // Store it
            _embeddingGenerator = embeddingGenerator;
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
        Content = string.Empty // Optionally populate later
    };

    var fileChunks = new List<FileChunk>();

    if (Path.GetExtension(newFile.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
    {
        using var pdf = PdfDocument.Open(newFile.OpenReadStream());
        var fullBuilder = new StringBuilder();

        foreach (Page page in pdf.GetPages())
        {
            string pageText = page.Text;
            fullBuilder.AppendLine(pageText);

            var embedding = await _embeddingGenerator.GenerateAsync(pageText);
            fileChunks.Add(new FileChunk
            {
                FileRecord = fileRecord,
                PageNumber = page.Number,
                Content = pageText,
                Embedding = new Vector(embedding.Vector.ToArray())
            });
        }

        fileRecord.Content = fullBuilder.ToString();
    }
    else
    {
        using var reader = new StreamReader(newFile.OpenReadStream());
        var text = await reader.ReadToEndAsync();

        var embedding = await _embeddingGenerator.GenerateAsync(text);
        fileChunks.Add(new FileChunk
        {
            FileRecord = fileRecord,
            PageNumber = 1,
            Content = text,
            Embedding = new Vector(embedding.Vector.ToArray())
        });

        fileRecord.Content = text;
    }

    await _db.FileRecords.AddAsync(fileRecord);
    await _db.FileChunks.AddRangeAsync(fileChunks);
    await _db.SaveChangesAsync();

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