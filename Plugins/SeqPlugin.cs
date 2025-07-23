using System.ComponentModel;
using Microsoft.SemanticKernel;
using Seq.Api;
using WebApplication2.Services;

namespace TodoApi.Plugins;

public class SeqPlugin
{
    
    private readonly IServiceProvider _serviceProvider;
    private readonly TodoService _todoService;
    private readonly SeqConnection _conn = new SeqConnection("http://localhost:32768", "UtnUVhWx91hv5x9xGIBz");
    
    [KernelFunction("GetLogs")]
    [Description("Fetch the event from SEQ using the provided filters")]
    public async Task<IEnumerable<string>> QueryLogs(string filters)
    {
        Console.WriteLine(filters);    
        var res = _conn.Events.EnumerateAsync(filter: filters, render: true);        
        List<string> logs = new List<string>();
        await foreach (var evt in res)
        {
            Console.WriteLine(evt.RenderedMessage);        
            logs.Add(evt.RenderedMessage);
        }
        return logs;
    }
    
    [KernelFunction("GetTemplates")]
    [Description("Gets all the possible Message templates of the SEQ events")]
    public async Task<IEnumerable<string>> GetSEQMessageStructure()
    {
        var res = await _conn.Data.QueryAsync("select distinct(@MessageTemplate) as MessageTemplate from stream");
        List<string> messageTemplates = new List<string>();
        foreach (var row in res.Rows)
            messageTemplates.Add((string)row[0]);

        return messageTemplates;

    }
}