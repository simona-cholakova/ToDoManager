using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;

namespace TodoApi.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class PromptController : ControllerBase
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletionService;

        public PromptController(Kernel kernel, IChatCompletionService chatService)
        {
            _kernel = kernel;
            _chatCompletionService = chatService;
        }

        [HttpPost]
        public async Task<IActionResult> PromptText([FromBody] string inputText)
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(inputText);
            var settings = new GeminiPromptExecutionSettings()
            {
                ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            };

            var res = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, settings, _kernel);
            return Ok(res[0].Content);
        }
    }
}
