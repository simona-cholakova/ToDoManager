using Microsoft.AspNetCore.Mvc;

namespace TodoApi.Models;

public class TodoItem
{
    public long Id { get; set; }
    public string UserId { get; set; } = null!;
    public string? Name { get; set; }
    
    public bool IsComplete { get; set; }

    public Pgvector.Vector Vector { get; set; } = new Pgvector.Vector(new float[786]);

}