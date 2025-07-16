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
            UserManager<User> userManager, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator) // Inject here
        {
            _db = db;
            _userManager = userManager; // Store it
            _embeddingGenerator = embeddingGenerator;
        }


        [HttpPost("add-file")]
        public async Task<IActionResult> AddFile(IFormFile newFile)
        {
            if (newFile == null || string.IsNullOrWhiteSpace(newFile.FileName))
            {
                return BadRequest("Invalid file data.");
            }

            string content;

            //if it is PDF, extract text using PdfPig
            if (Path.GetExtension(newFile.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                using var pdf = PdfDocument.Open(newFile.OpenReadStream());
                var builder = new StringBuilder();

                foreach (Page page in pdf.GetPages())
                {
                    builder.AppendLine(page.Text);
                }

                content = builder.ToString();
            }
            else
            {
                //not PDF
                var builder = new StringBuilder();
                using (var reader = new StreamReader(newFile.OpenReadStream()))
                {
                    while (reader.Peek() >= 0)
                        builder.AppendLine(await reader.ReadLineAsync());
                }

                content = builder.ToString();
            }

            //embed
            var embeddingMemory = await _embeddingGenerator.GenerateAsync(content);
            ReadOnlyMemory<float> embedding = embeddingMemory.Vector;

            var file = new FileRecord
            {
                FileName = newFile.FileName,
                Content = content,
                Embedding = new Vector(embedding.ToArray()), // Pgvector.Vector
            };

            //if file already exists
            var existing = await _db.FileRecords.FirstOrDefaultAsync(f => f.FileName == file.FileName);
            if (existing != null)
            {
                return BadRequest($"File '{file.FileName}' already exists.");
            }

            _db.FileRecords.Add(file);
            await _db.SaveChangesAsync();

            return Ok($"File '{file.FileName}' was added successfully.");
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