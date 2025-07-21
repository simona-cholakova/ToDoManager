using Microsoft.EntityFrameworkCore;
using TodoApi.Models;
using Microsoft.SemanticKernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using TodoApi.Plugins;
using WebApplication2.Services;

var builder = WebApplication.CreateBuilder(args);

// Load secrets
builder.Configuration
    .AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true);

// Load keys and model IDs
var openAiKey = builder.Configuration["OpenAi:ApiKey"];
var openAiModel = builder.Configuration["OpenAi:ModelId"];
var openAiEmbeddingModel = builder.Configuration["OpenAi:EmbeddingModelId"];

// Register OpenAI embedding generation service
builder.Services.AddOpenAITextEmbeddingGeneration(
    modelId: openAiEmbeddingModel,
    apiKey: openAiKey
);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IdentityConstants.BearerScheme;
    options.DefaultChallengeScheme = IdentityConstants.BearerScheme;
    options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
})
.AddCookie(IdentityConstants.ApplicationScheme)
.AddBearerToken(IdentityConstants.BearerScheme);

builder.Services.AddAuthorizationBuilder();

builder.Services.AddDbContext<TodoContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TodoContext"), o => o.UseVector()));

builder.Services.AddIdentityCore<User>()
    .AddEntityFrameworkStores<TodoContext>()
    .AddApiEndpoints();

// Register Semantic Kernel
builder.Services.AddScoped<Kernel>(sp =>
{
    var embeddingGenerator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

    var kernelBuilder = Kernel.CreateBuilder()
        .AddOpenAIChatCompletion(modelId: openAiModel, apiKey: openAiKey);
        
    var kernel = kernelBuilder.Build();

    var dbContext = sp.GetRequiredService<TodoContext>();

    var filePlugin = new FilePlugin(sp, dbContext, embeddingGenerator);
    var todoPlugin = new ToDoPlugin(sp, dbContext, embeddingGenerator, new TodoService(dbContext, new HttpContextAccessor()));

    kernel.Plugins.AddFromObject(filePlugin, "FilePlugin");
    kernel.Plugins.AddFromObject(todoPlugin, "ToDoPlugin");

    return kernel;
});

#pragma warning disable SKEXP0010
builder.Services.AddOpenAIEmbeddingGenerator(modelId:openAiEmbeddingModel, apiKey:openAiKey);
#pragma warning restore SKEXP0010

// Expose chat completion service separately if needed
builder.Services.AddScoped(sp =>
{
    var kernel = sp.GetRequiredService<Kernel>();
    return kernel.GetRequiredService<IChatCompletionService>();
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityApi<User>();
app.MapControllers();

app.Run();
