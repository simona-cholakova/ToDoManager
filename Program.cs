using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.OpenApi.Models;
using TodoApi.Models;
using TodoApi.Plugins;
using WebApplication2.Services;

var builder = WebApplication.CreateBuilder(args);

// ────── Load Secrets ──────
builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true);

var openAiKey = builder.Configuration["OpenAi:ApiKey"];
var openAiModel = builder.Configuration["OpenAi:ModelId"];
var openAiEmbeddingModel = builder.Configuration["OpenAi:EmbeddingModelId"];

var geminiKey = builder.Configuration["Gemini:ApiKey"];
var geminiModel = builder.Configuration["Gemini:ModelId"];

// ────── Register Embedding Service ──────
builder.Services.AddOpenAITextEmbeddingGeneration(
    modelId: openAiEmbeddingModel,
    apiKey: openAiKey
);

#pragma warning disable SKEXP0010
builder.Services.AddOpenAIEmbeddingGenerator(modelId: openAiEmbeddingModel, apiKey: openAiKey);
#pragma warning restore SKEXP0010

// ────── Register ASP.NET Services ──────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ────── Swagger Setup with Auth ──────
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" });

    // JWT Bearer auth
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Bearer token. Example: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    // Cookie auth
    c.AddSecurityDefinition("cookieAuth", new OpenApiSecurityScheme
    {
        Name = ".AspNetCore.Identity.Application",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Description = "Cookie-based authentication"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "cookieAuth" } },
            Array.Empty<string>()
        }
    });
});

// ────── Authentication & Authorization ──────
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IdentityConstants.BearerScheme;
    options.DefaultChallengeScheme = IdentityConstants.BearerScheme;
    options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
})
.AddCookie(IdentityConstants.ApplicationScheme)
.AddBearerToken(IdentityConstants.BearerScheme);

builder.Services.AddAuthorizationBuilder();

// ────── EF Core & Identity Setup ──────
builder.Services.AddDbContext<TodoContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TodoContext"), o => o.UseVector()));

builder.Services.AddIdentityCore<User>()
    .AddEntityFrameworkStores<TodoContext>()
    .AddApiEndpoints();

// ────── Semantic Kernel & Plugins Setup ──────
builder.Services.AddScoped<Kernel>(sp =>
{
    var embeddingGenerator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

    var kernel = Kernel.CreateBuilder()
        .AddOpenAIChatCompletion(modelId: openAiModel, apiKey: openAiKey)
        .AddGoogleAIGeminiChatCompletion(modelId: geminiModel, apiKey: geminiKey)
        .Build();

    var dbContext = sp.GetRequiredService<TodoContext>();
    var filePlugin = new FilePlugin(sp, dbContext, embeddingGenerator);
    var todoPlugin = new ToDoPlugin(sp, dbContext, embeddingGenerator, new TodoService(dbContext, new HttpContextAccessor()));
    var seqPlugin = new SeqPlugin();

    kernel.Plugins.AddFromObject(filePlugin, "FilePlugin");
    kernel.Plugins.AddFromObject(todoPlugin, "ToDoPlugin");
    kernel.Plugins.AddFromObject(seqPlugin, "SeqPlugin");

    return kernel;
});

// ────── Optional Utility Services ──────
builder.Services.AddScoped<AIOptionService>();

// ────── Use OpenAI Chat by Default ──────
builder.Services.AddScoped<IChatCompletionService>(sp =>
{
    var kernel = sp.GetRequiredService<Kernel>();
    return kernel.GetRequiredService<IChatCompletionService>(); // No key needed
});

// ────── CORS for Frontend ──────
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

// ────── Middleware ──────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1"));
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityApi<User>();
app.MapControllers();

app.Run();
