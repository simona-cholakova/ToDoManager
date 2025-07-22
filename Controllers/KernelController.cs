using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using TodoApi.Models;
using TodoApi.Plugins;

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
        
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PromptText([FromBody] string inputText)
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("Running `searchFileContent` to locate relevant document content or run GetLogs to create SEQ queries and fetch some data based on a parameter.");

            string userId = _userManager.GetUserId(User);

            // Load recent user chat history
            List<UserContextHistory> userChat = await _context.UserContextHistory
                .Where(h => h.userId == userId)
                .OrderByDescending(h => h.Id)
                .Take(10)
                .ToListAsync();

            userChat.Reverse(); // oldest to newest
            foreach (var prompt in userChat)
            {
                chatHistory.AddUserMessage(prompt.userPrompt);
                chatHistory.AddAssistantMessage(prompt.agentResponse);
                chatHistory.AddSystemMessage("Use searchFileContent for retrieving information. Use GetLogs forwriting SEQ query and fetching information based on the specified parameter.");
            }

            // Embed current prompt
            var promptEmbedding = await _embeddingGenerator.GenerateAsync(inputText);
            var userVector = promptEmbedding.Vector.ToArray();

            // Get all file chunks with their embeddings
            var allChunks = await _context.FileChunks
                .Include(fc => fc.FileRecord) // Include related file metadata
                .ToListAsync();

            // Find the most similar chunk by cosine similarity
            var topMatch = allChunks
                .Select(chunk => new
                {
                    Chunk = chunk,
                    Similarity = CosineSimilarity(userVector, chunk.Embedding.ToArray())
                })
                .OrderByDescending(x => x.Similarity)
                .FirstOrDefault();

            Console.WriteLine($"Top match chunk from file: {topMatch?.Chunk.FileRecord.FileName}, Page: {topMatch?.Chunk.PageNumber}, Similarity: {topMatch?.Similarity}");

            

            // Add user message last
            chatHistory.AddUserMessage(inputText);

            var settings = new GeminiPromptExecutionSettings
            {
                ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            };

            var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, settings, _kernel);

            // Save conversation to DB
            var userContextHistory = new UserContextHistory
            {
                userPrompt = inputText,
                userId = userId,
                agentResponse = result[0].Content
            };
            _context.UserContextHistory.Add(userContextHistory);
            await _context.SaveChangesAsync();

            return Ok(result[0].Content);
        }

        
        private static float CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length) return 0f;

            float dot = 0f;
            float magA = 0f;
            float magB = 0f;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dot += vectorA[i] * vectorB[i];
                magA += vectorA[i] * vectorA[i];
                magB += vectorB[i] * vectorB[i];
            }
            

            return dot / (float)(Math.Sqrt(magA) * Math.Sqrt(magB) + 1e-8); // Add epsilon to avoid division by zero
        }

    }
}
