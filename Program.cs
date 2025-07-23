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
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "API", Version = "v1" });

    // Bearer token authentication
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Bearer token. Example: Bearer {token}",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    // Cookie authentication
    c.AddSecurityDefinition("cookieAuth", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        In = Microsoft.OpenApi.Models.ParameterLocation.Cookie,
        Name = ".AspNetCore.Identity.Application", // Default cookie name for Identity
        Description = "Cookie-based auth"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        },
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "cookieAuth"
                }
            },
            new string[] {}
        }
    });
});


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
    var seqPlugin = new SeqPlugin();

    kernel.Plugins.AddFromObject(filePlugin, "FilePlugin");
    kernel.Plugins.AddFromObject(todoPlugin, "ToDoPlugin");
    kernel.Plugins.AddFromObject(seqPlugin, "SeqPlugin");

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

//CORS
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
