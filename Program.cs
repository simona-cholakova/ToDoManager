using Microsoft.EntityFrameworkCore;
using TodoApi.Models;
using Microsoft.SemanticKernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.SemanticKernel.ChatCompletion;

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
    opt.UseNpgsql(builder.Configuration.GetConnectionString("TodoContext")));

builder.Services.AddScoped(sp =>
{
    kernelBuilder.AddGoogleAIGeminiChatCompletion(
        modelId: geminiModel,
        apiKey: geminiKey);
    
    // Build the kernel from the builder
    var kernel = kernelBuilder.Build();

    // Register NativeFunctions with DI (passing IServiceProvider)
    kernel.Plugins.AddFromObject(new NativeFunctions(sp), "NativeFunctions");

    return kernel;
});

builder.Services.AddScoped(sp => {var kernel = sp.GetRequiredService<Kernel>();    return kernel.GetRequiredService<IChatCompletionService>();});


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

app.UseAuthorization();

app.UseAuthentication();

app.MapControllers();

app.MapIdentityApi<User>();

app.Run();