using System.ComponentModel;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using TodoApi.Models;

namespace WebApplication2.Services;

public class TodoService
{
    private readonly TodoContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TodoService(TodoContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    private string GetUserId()
    {
        string userid = _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value;
        return userid;
    }
    public async Task<IEnumerable<TodoItem>> GetAllTodos()
    {
      List<TodoItem> todos = await _context.ToDoItems.Where(p => p.UserId == GetUserId()).ToListAsync();

      return todos;
    }

    public async Task<bool> AddTodo(bool isComplete, string task)
    {
        TodoItem todoItem = new TodoItem { UserId = GetUserId(), IsComplete = isComplete, Name = task };
        await _context.ToDoItems.AddAsync(todoItem);
     
        int changes = await _context.SaveChangesAsync();

        if (changes > 0)
        {
            return true;
        }
     
        return false;
    }

    public TodoItem GetTodoItem(int todoid)
    {
        var todoItem = _context.ToDoItems.Find(todoid);

        if (todoItem == null)
        {
            return null;
        }

        return todoItem;
    }

    public async Task<bool> DeleteTodoItem(string taskName)
    {
        var todoItem = await _context.ToDoItems
            .FirstOrDefaultAsync(t => t.Name != null && t.Name.Contains(taskName));

        if (todoItem == null)
        {
            return false;
        }

        _context.ToDoItems.Remove(todoItem);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> UpdateTodoItem(string taskName)
    {
        var todoItem = await _context.ToDoItems
            .FirstOrDefaultAsync(t => t.Name != null && t.Name.Contains(taskName));
        
        if (todoItem == null || todoItem.UserId != GetUserId())
        {
            return false;
        }
      
        todoItem.IsComplete = true;


        if (_context.SaveChanges() > 0)
        {
            return true;
        }

        return false;


    }
}