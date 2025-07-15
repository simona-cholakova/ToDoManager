using Microsoft.EntityFrameworkCore;
using TodoApi.Models;
using Microsoft.SemanticKernel;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

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

kernelBuilder.Services.AddGoogleAIGeminiChatCompletion(
    modelId: geminiModel,
    apiKey: geminiKey
);

var kernel = kernelBuilder.Build();
kernel.Plugins.AddFromObject(new NativeFunctions(), "NativeFunctions"); // Register here!
builder.Services.AddSingleton(kernel);


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