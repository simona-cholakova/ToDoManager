namespace TodoApi.Models;

public class FileRecord
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
