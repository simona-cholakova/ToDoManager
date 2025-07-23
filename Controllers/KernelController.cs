using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TodoApi.Models;
using WebApplication2.Services;

namespace TodoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PromptController : ControllerBase
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletionService;
        private readonly UserManager<User> _userManager;
        private readonly TodoContext _context;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
        private readonly IServiceProvider _serviceProvider;

        public PromptController(
            Kernel kernel,
            IChatCompletionService chatService,
            UserManager<User> userManager,
            TodoContext context,
            IServiceProvider serviceProvider,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
        {
            _kernel = kernel;
            _chatCompletionService = chatService;
            _userManager = userManager;
            _context = context;
            _embeddingGenerator = embeddingGenerator;
            _serviceProvider = serviceProvider;
        }

        // 🌐 General Chat Prompt
        [Authorize]
        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] string inputText)
        {
            var chatHistory = await BuildChatHistory(inputText);
            var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            }, _kernel);

            await SaveHistory(inputText, result[0].Content);
            return Ok(result[0].Content);
        }

        [Authorize]
        [HttpPost("logs")]
        public async Task<IActionResult> HandleSeqLogs([FromBody] string inputText)
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are a SEQ log analyzer. Generate or interpret SEQ queries.");
            chatHistory.AddUserMessage(inputText);

            KernelFunction getLogs = _kernel.Plugins.GetFunction("SeqPlugin", "GetLogs");
            KernelFunction getTemplates = _kernel.Plugins.GetFunction("SeqPlugin", "GetTemplates");

            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(functions: [getTemplates, getLogs])
            };

            var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, settings, _kernel);

            return Ok(result[0].Content);
        }


        // ✅ ToDos Query
        [Authorize]
        [HttpPost("todos")]
        public async Task<IActionResult> HandleTodos([FromBody] string inputText)
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("Use the ToDoPlugin to retrieve or manage user's todo items.");
            chatHistory.AddUserMessage(inputText);

            KernelFunction getTodos = _kernel.Plugins.GetFunction("ToDoPlugin", "GetAllTodos");
            KernelFunction createTodo = _kernel.Plugins.GetFunction("ToDoPlugin", "createTodo");
            KernelFunction deleteToDo = _kernel.Plugins.GetFunction("ToDoPlugin", "deleteToDoItem");

            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(functions: [getTodos, createTodo, deleteToDo])
            };

            var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, settings, _kernel);

            return Ok(result[0].Content);
        }


        // 📜 Rules-Based Logic
        [Authorize]
        [HttpPost("rules")]
        public async Task<IActionResult> HandleRules([FromBody] string inputText)
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are a rule-based assistant. Evaluate or apply rules based on user input.");
            chatHistory.AddUserMessage(inputText);
            
            KernelFunction searchFiles = _kernel.Plugins.GetFunction("FilePlugin", "searchFileContent");

            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(functions: [searchFiles])
            };
            
            var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            }, _kernel);

            return Ok(result[0].Content);
        }

        // 🔄 Shared helpers

        private async Task<ChatHistory> BuildChatHistory(string inputText)
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("Use searchFileContent for retrieving information. Use GetLogs for writing SEQ query and fetching information based on the specified parameter.");

            string userId = _userManager.GetUserId(User);
            var userChat = await _context.UserContextHistory
                .Where(h => h.userId == userId)
                .OrderByDescending(h => h.Id)
                .Take(10)
                .ToListAsync();

            userChat.Reverse();
            foreach (var prompt in userChat)
            {
                chatHistory.AddUserMessage(prompt.userPrompt);
                chatHistory.AddAssistantMessage(prompt.agentResponse);
            }

            chatHistory.AddUserMessage(inputText);
            return chatHistory;
        }

        private async Task SaveHistory(string inputText, string response)
        {
            string userId = _userManager.GetUserId(User);
            var userContextHistory = new UserContextHistory
            {
                userPrompt = inputText,
                userId = userId,
                agentResponse = response
            };

            _context.UserContextHistory.Add(userContextHistory);
            await _context.SaveChangesAsync();
        }
    }
}
