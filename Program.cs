using Microsoft.EntityFrameworkCore;
using TodoApi.Models;
using Microsoft.SemanticKernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.ChatCompletion;
using TodoApi.Plugins;
using WebApplication2.Services;

var builder = WebApplication.CreateBuilder(args);
var kernelBuilder = Kernel.CreateBuilder();

// Create SK kernel
var geminiKey = builder.Configuration["Gemini:ApiKey"];
var geminiModel = builder.Configuration["Gemini:ModelId"];

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthentication().AddCookie(IdentityConstants.ApplicationScheme);
builder.Services.AddAuthorization();
builder.Services.AddIdentityCore<User>().AddEntityFrameworkStores<TodoContext>().AddApiEndpoints();

// Replace in-memory DB with PostgreSQL
builder.Services.AddDbContext<TodoContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("TodoContext"), (o) => o.UseVector()
    ));

builder.Services.AddScoped(sp =>
{
    kernelBuilder.AddGoogleAIGeminiChatCompletion(
        modelId: geminiModel,
        apiKey: geminiKey);

    var kernel = kernelBuilder.Build();

    var dbContext = sp.GetRequiredService<TodoContext>();
    var embeddingGenerator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

    var filePlugin = new FilePlugin(sp, dbContext, embeddingGenerator);
    var todoPlugin = new ToDoPlugin(sp,  dbContext, embeddingGenerator, new TodoService(dbContext, new HttpContextAccessor()));
    
    kernel.Plugins.AddFromObject(filePlugin, "FilePlugin");
    kernel.Plugins.AddFromObject(todoPlugin, "ToDoPlugin");
    
    return kernel;
});


builder.Services.AddGoogleAIEmbeddingGenerator(
    modelId: "text-embedding-004",
    apiKey: "AIzaSyDMVewunSABShabhXiJcKNxI5Yi95OCzLU"
);

// builder.Services.AddCors(options =>
// {
//     options.AddPolicy("AllowFrontend",
//         policy => policy.WithOrigins("http://localhost:3000")
//             .AllowAnyHeader()
//             .AllowAnyMethod());
// });
builder.Services.AddCors();


builder.Services.AddScoped(sp => {var kernel = sp.GetRequiredService<Kernel>();    
    return kernel.GetRequiredService<IChatCompletionService>();});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
    });
}

app.UseHttpsRedirection();

// app.UseCors("AllowFrontend");

app.UseAuthorization();

app.UseAuthentication();

app.MapControllers();

app.MapIdentityApi<User>();

app.Run();