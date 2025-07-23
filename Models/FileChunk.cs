using System.ComponentModel.DataAnnotations.Schema;

namespace TodoApi.Models;

public class FileChunk
{
    public int Id { get; set; }

    public int FileRecordId { get; set; }
    public FileRecord FileRecord { get; set; } = default!;

    public int PageNumber { get; set; }

    public string Content { get; set; } = string.Empty;

    [Column(TypeName = "vector(1536)")]
    public Pgvector.Vector Embedding { get; set; } = default!;
    
    public int? ClusterID { get; set; }

    public string? ClusterMethod { get; set; }

}