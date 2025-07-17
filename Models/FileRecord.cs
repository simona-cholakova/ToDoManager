using System.ComponentModel.DataAnnotations.Schema;

namespace TodoApi.Models;

public class FileRecord
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    
    [Column(TypeName = "vector(768)")]
    public Pgvector.Vector Embedding { get; set; } //PostgreSQL vector extension

}
