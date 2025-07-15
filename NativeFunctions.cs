using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using TodoApi.Models;

public class NativeFunctions
{
    private readonly IServiceProvider _serviceProvider;

    public NativeFunctions(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    [KernelFunction]
    public async Task<string> RetrieveLocalFileAsync(string fileName, int maxSize = 5000)
    {
        string basePath = AppContext.BaseDirectory;
        string filePath = Path.Combine(basePath, fileName);
        if (!File.Exists(filePath))
        {
            return $"File '{fileName}' not found.";
        }

        string content = await File.ReadAllTextAsync(fileName);
        return content.Length <= maxSize ? content : content.Substring(0, maxSize);
    }
    
    [KernelFunction]
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