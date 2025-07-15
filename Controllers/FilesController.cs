using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoApi.Models;

namespace TodoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly TodoContext _db;

        public FilesController(TodoContext db)
        {
            _db = db;
        }

        [HttpPost("add-file")]
        public async Task<IActionResult> AddFile([FromBody] FileRecord file)
        {
            if (file == null || string.IsNullOrWhiteSpace(file.FileName))
            {
                return BadRequest("Invalid file data.");
            }

            var existing = await _db.FileRecords.FirstOrDefaultAsync(f => f.FileName == file.FileName);
            if (existing != null)
            {
                return BadRequest($"File '{file.FileName}' already exists.");
            }

            _db.FileRecords.Add(file);
            await _db.SaveChangesAsync();

            return Ok($"File '{file.FileName}' was added successfully.");
        }
    }
}