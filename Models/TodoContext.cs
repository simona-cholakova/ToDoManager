using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.General;
using Pgvector;

namespace TodoApi.Models;


public class TodoContext : IdentityDbContext<User>
{
    public TodoContext(DbContextOptions<TodoContext> options)
        : base(options)
    {
    }

    public DbSet<TodoItem> ToDoItems { get; set; } = null!;
    public DbSet<FileRecord> FileRecords { get; set; } = null!;
    public DbSet<FileChunk> FileChunks { get; set; }
    public DbSet<UserContextHistory> UserContextHistory { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<FileChunk>()
            .HasOne(c => c.FileRecord)
            .WithMany(f => f.Chunks)
            .HasForeignKey(c => c.FileRecordId)
            .OnDelete(DeleteBehavior.Cascade);
    }

}

