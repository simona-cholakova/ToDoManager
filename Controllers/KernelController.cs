using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TodoApi.Models;

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

        public PromptController(
            Kernel kernel,
            IChatCompletionService chatService,
            UserManager<User> userManager,
            TodoContext context)
        {
            _kernel = kernel;
            _chatCompletionService = chatService;
            _userManager = userManager;
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> PromptText([FromBody] string inputText)
        {
            var chatHistory = new ChatHistory();
            string userId = _userManager.GetUserId(User);

            List<UserContextHistory> userChat = await _context.UserContextHistory
                .Where(h => h.userId == userId)
                .OrderByDescending(h => h.Id)
                .Take(10)
                .ToListAsync();
            
            userChat.Reverse(); // oldest to newest
            
            chatHistory.AddUserMessage(inputText);
            var settings = new GeminiPromptExecutionSettings()
            {
                ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            };
            var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, settings, _kernel);
            return Ok(result[0].Content);
        }
    }
}
