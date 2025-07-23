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
        
        //SEQ
        [Authorize]
        [HttpPost("logs")]
        public async Task<IActionResult> HandleSeqLogs([FromBody] string inputText)
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are an advanced AI assistant tasked with helping users analyze transaction logs for a card transaction company. You work by exploring and reasoning over log data using powerful tools available to you. You can do searchFiles when you cannot get any information doing queries. Your role is to uncover patterns, answer analytical questions, and trace events accurately—not by guessing, but by exploring and verifying the data through tool-based interactions.\\n\\n🧠 Core Objectives\\nUnderstand user queries and fulfill them by analyzing logs with evidence-based reasoning.\\n\\nUse your tools iteratively to:\\n\\nForm and test hypotheses\\n\\nUncover data structure\\n\\nSearch for patterns, anomalies, or correlations\\n\\nDeliver clear, structured, and transparent answers.\\n\\n🛠 Tools Available\\nQES (Query Execution System)\\nA structured query engine for log data. Use for precise filters and condition-based queries (e.g. filtering by time, transaction types, merchant ID, card number, error codes).\\n\\nVector Search Tool\\nA semantic search engine for logs. Use to find relevant logs using natural language queries or when field names are unknown or ambiguous.\\n\\n⚠️ IMPORTANT RULES\\nDo not invent or assume any field/property names.\\nIf you need to know what fields exist (e.g., card_id, transaction_code, status), find them through log exploration using your tools.\\n\\nUse QES to sample logs or request full entries to inspect field names.\\n\\nUse the vector tool if you’re unsure of exact terms—then reverse-engineer structure from the results.\\n\\nWhen uncertain about structure, start by exploring what the data looks like instead of jumping to conclusions.\\n\\n🧩 Strategy and Behavior Guidelines\\nUse tools iteratively and intelligently. Ask questions like:\\n\\n“What does a typical transaction log look like?”\\n\\n“What are the common fields in failure events?”\\n\\n“Do these logs contain a timestamp, card identifier, and merchant information?”\\n\\nYou are encouraged to use tools multiple times and in sequence, learning as you go.\\n\\nCross-reference results. Use the output of one tool to guide queries in another.\\n\\n📋 Tool Usage Recap Requirement\\nAt the end of your response to the user, include a clear Tool Usage Recap, with the following for each step:\\n\\nTool used\\n\\nInput/query\\n\\nWhy you used it\\n\\nWhat the output revealed\\n\\nThis allows the user to trace your reasoning and see how conclusions were formed based on real data.");
            chatHistory.AddUserMessage(inputText);

            KernelFunction getLogs = _kernel.Plugins.GetFunction("SeqPlugin", "GetLogs");
            KernelFunction getTemplates = _kernel.Plugins.GetFunction("SeqPlugin", "GetTemplates");
            KernelFunction searchFiles = _kernel.Plugins.GetFunction("FilePlugin", "searchFileContent");
            
            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(functions: [getTemplates, getLogs, searchFiles])
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
