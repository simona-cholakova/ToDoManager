using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using TodoApi.Models;

public class NativeFunctions
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TodoContext _context;

    public NativeFunctions(IServiceProvider serviceProvider, TodoContext context)
    {
        _serviceProvider = serviceProvider;
        _context = context;
    }

    [KernelFunction, Description("Reads the content of a file stored on disk by name. Useful when a user references a file in their question or something related to the file")]
    public async Task<string> RetrieveFileFromDatabaseAsync(string fileName, int maxSize = 5000)
    {
        Console.WriteLine($"Database file function was invoked with: {fileName}");

        var file = await _context.FileRecords
            .FirstOrDefaultAsync(f => f.FileName == fileName);

        if (file == null)
        {
            return $"File '{fileName}' not found in database.";
        }

        return file.Content.Length <= maxSize
            ? file.Content
            : file.Content.Substring(0, maxSize);
    }
    
    [KernelFunction, Description("This function gets all todo tasks")]
    public async Task<string> GetAllTodoTasksAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TodoContext>();

        var tasks = await db.ToDoItems.ToListAsync();
        if (tasks.Count == 0) return "You have no todo tasks.";

        var result = string.Join("\n", tasks.Select(t => $"- {t.Name}"));
        return result;
    }

}